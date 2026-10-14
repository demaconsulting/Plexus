using System.Text;
using System.Text.Json;

namespace DemaConsulting.Plexus;

/// <summary>
///     Copies elements to a portable JSON string and pastes such strings into a page.
/// </summary>
/// <remarks>
///     The string is independent of the diagram it came from, so it can travel through the system
///     clipboard between applications or diagrams. A host that is not the Avalonia editor registers <see cref="FormatId"/>
///     as its application-specific clipboard format (or MIME type) AND also offers the same JSON as plain text, because text
///     is what other applications and remote sessions forward; on paste, read the specific format first and fall back to text.
///     Pasting treats the text as untrusted: it is limited to 8 MiB,
///     10,000 elements and 64 nesting levels, contains no type names, is validated completely before
///     anything changes, and then applied as one undoable "Paste" step. Core performs no clipboard I/O.
///     Port lists are not copied: a pasted node gets the ports its type's <see cref="NodeType.PortProvider"/> computes
///     from the pasted properties and data before its wires are recreated, and a wire naming a port that does not exist
///     then refuses the paste with a message, like any unknown port.
/// </remarks>
/// <example>
///     Copy a selection to a string and paste it, here into another page of the same diagram. The string can equally go through the system clipboard.
///     <code>
///     using DemaConsulting.Plexus;
///     using DemaConsulting.Plexus.Geometry;
///
///     var diagram = new Diagram(types);                    // types: the registry of the DemaConsulting.Plexus example
///     var page = diagram.Pages.Add("Main");
///     var a = page.AddNode("demo.Source", 0, 0, name: "A");
///     var b = page.AddNode("demo.Sink", 180, 0, name: "B");
///     page.Connect("A.out", "B.in");
///
///     string text = PlexusClipboard.Copy([a, b]);          // nodes plus the wires between them
///
///     var target = diagram.Pages.Add("Copy target");
///     PasteResult result = PlexusClipboard.Paste(target, text, new Point(60, 60));
///     if (result.Success)
///     {
///         Console.WriteLine($"pasted {result.Elements.Count} elements");   // one undo entry
///     }
///     else
///     {
///         // Untrusted text never throws: the page is untouched and the result says why
///         Console.WriteLine(result.Error);
///         Console.WriteLine(string.Join(", ", result.MissingNodeTypes));
///     }
///
///     // Whole documents use JSON instead of the clipboard
///     var copy = Diagram.FromJson(diagram.ToJson(), types);
///     </code>
/// </example>
public static class PlexusClipboard
{
    /// <summary>
    ///     The clipboard format identifier (also the value of the <c>format</c> member in the JSON).
    /// </summary>
    public const string FormatId = "plexus.clipboard";

    /// <summary>
    ///     The clipboard payload version written and accepted.
    /// </summary>
    public const int FormatVersion = 1;

    /// <summary>
    ///     The largest clipboard text accepted by <see cref="Paste"/>, in bytes of UTF-8 (8 MiB).
    /// </summary>
    public const int MaxPayloadBytes = 8 * 1024 * 1024;

    /// <summary>
    ///     The largest number of elements accepted by <see cref="Paste"/>.
    /// </summary>
    public const int MaxElements = 10_000;

    /// <summary>
    ///     Serializes elements to clipboard text.
    /// </summary>
    /// <remarks>
    ///     Selecting a group copies its members recursively. A wire is included when both of its nodes
    ///     are copied (whether or not the wire itself was selected); a selected wire alone is not copied.
    ///     Node payloads are included for node types declaring <see cref="NodeType.DataType"/> or
    ///     <see cref="NodeType.DataSerializer"/> and omitted otherwise. Copying does not change the diagram.
    /// </remarks>
    /// <param name="elements">The elements to copy; all must be on one page of a diagram. May be empty.</param>
    /// <returns>The clipboard JSON.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="elements"/> is null.</exception>
    /// <exception cref="ArgumentException">Thrown when an element is null, not on a page, or the elements are on different pages.</exception>
    /// <exception cref="InvalidOperationException">Thrown when a node payload does not match its declared data type or serializes to invalid JSON.</exception>
    public static string Copy(IEnumerable<DiagramElement> elements)
    {
        var carried = ClipboardSelection.Expand(elements, out _);
        var content = SnapshotWriter.Capture(carried, keepDataReferences: false);
        var envelope = new ClipboardEnvelope
        {
            Format = FormatId,
            Version = FormatVersion,
            Nodes = content.Nodes!,
            Wires = content.Wires!,
            Annotations = content.Annotations!,
            Groups = content.Groups!,
            ZOrder = content.ZOrder,
        };
        return JsonSerializer.Serialize(envelope, PlexusJson.Compact.ClipboardEnvelope);
    }

    /// <summary>
    ///     Pastes clipboard text into a page as one undoable step.
    /// </summary>
    /// <remarks>
    ///     <list type="bullet">
    ///         <item>Validation is complete before anything is changed: on failure the page and history are untouched.</item>
    ///         <item>Unknown node types fail the paste and are all listed in <see cref="PasteResult.MissingNodeTypes"/>.</item>
    ///         <item>Ids are regenerated; node names are made unique with a numeric suffix when the diagram requires unique names.</item>
    ///         <item>Property values whose kind does not fit the target definition fall back to the default and add a warning.</item>
    ///         <item><paramref name="at"/> is the TOP-LEFT corner of the pasted content's overall bounds (the union of the pasted node and annotation boxes and collapsed group boxes; names, port labels and wire routes do not count), not the position of any single element or the pointer hot spot; the whole selection keeps its relative layout. When null the content is offset by one grid step from its original position.</item>
    ///         <item>Stored wire routes are translated with their nodes (the router is not consulted); wires the connection rules reject are skipped with a warning.</item>
    ///     </list>
    /// </remarks>
    /// <param name="target">The page to paste into.</param>
    /// <param name="json">The clipboard text produced by <see cref="Copy"/>; untrusted.</param>
    /// <param name="at">The diagram position of the top-left corner of the pasted content's bounds; null offsets the content by one grid step from its original position.</param>
    /// <returns>The outcome; invalid content yields a failed result rather than an exception.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="target"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="at"/> is not finite or exceeds 1e9 in magnitude.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the target page is removed from its diagram.</exception>
    public static PasteResult Paste(DiagramPage target, string json, Point? at = null)
    {
        ArgumentNullException.ThrowIfNull(target);
        target.EnsureAttached();
        if (at is { } position)
        {
            DiagramPage.EnsureOffset(position.X, nameof(at));
            DiagramPage.EnsureOffset(position.Y, nameof(at));
        }

        // Everything about the text is untrusted: size, syntax, marker, version, then content
        var envelope = ReadEnvelope(json, out var problem);
        if (envelope is null)
        {
            return PasteResult.Failed(problem ?? "The clipboard content is not valid.");
        }

        var options = new ImportOptions
        {
            MaxElements = MaxElements,
            At = at,
            Dx = target.Diagram.Theme.GridSize,
            Dy = target.Diagram.Theme.GridSize,
        };
        var prepared = ElementImporter.Prepare(target, envelope.Content, options);
        if (!prepared.Success)
        {
            return PasteResult.Failed(prepared.Error ?? "The clipboard content is not valid.", prepared.MissingNodeTypes);
        }

        // Validation passed: the only mutation is this single undoable operation
        var created = new List<DiagramElement>();
        target.Diagram.Perform("Paste", plan => created = prepared.Apply(target, plan, exactZOrder: false));
        return PasteResult.Succeeded(created, prepared.Warnings);
    }

    /// <summary>
    ///     Parses and checks the clipboard envelope.
    /// </summary>
    /// <param name="json">The clipboard text.</param>
    /// <param name="problem">Receives why the text was rejected.</param>
    /// <returns>The envelope, or null when the text is not acceptable Plexus clipboard content.</returns>
    private static ClipboardEnvelope? ReadEnvelope(string? json, out string? problem)
    {
        problem = null;
        if (string.IsNullOrWhiteSpace(json))
        {
            problem = "The clipboard text is empty.";
            return null;
        }

        // The character count is a cheap lower bound of the byte count, so oversized text is rejected without encoding it
        if (json.Length > MaxPayloadBytes || Encoding.UTF8.GetByteCount(json) > MaxPayloadBytes)
        {
            problem = $"The clipboard text is larger than the {MaxPayloadBytes / (1024 * 1024)} MiB limit.";
            return null;
        }

        ClipboardEnvelope? envelope;
        try
        {
            envelope = JsonSerializer.Deserialize(json, PlexusJson.Compact.ClipboardEnvelope);
        }
        catch (Exception ex) when (ex is JsonException or NotSupportedException or ArgumentException)
        {
            problem = $"The clipboard text is not valid Plexus data: {ex.Message}";
            return null;
        }

        if (envelope is null || !string.Equals(envelope.Format, FormatId, StringComparison.Ordinal))
        {
            problem = $"The clipboard text is not Plexus clipboard data (expected format '{FormatId}').";
            return null;
        }

        if (envelope.Version != FormatVersion)
        {
            problem = $"Unsupported Plexus clipboard version {envelope.Version}; version {FormatVersion} is supported.";
            return null;
        }

        return envelope;
    }
}
