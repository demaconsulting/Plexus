using System.Collections.Specialized;
using DemaConsulting.Plexus.Avalonia;
using DemaConsulting.Plexus.Demo.Model;
using DemaConsulting.Plexus.Rendering;
using PlexusPoint = DemaConsulting.Plexus.Geometry.Point;
using PlexusRect = DemaConsulting.Plexus.Geometry.Rect;

namespace DemaConsulting.Plexus.Demo.Gui;

/// <summary>
///     The demo's document controller: owns the current diagram and its file, and implements every
///     editing command once, for both the menus and the automation endpoint.
/// </summary>
/// <remarks>
///     The window and the automation dispatcher are thin layers over this class, so a command
///     behaves identically however it is triggered. It is single-threaded like the model: use it
///     from the UI thread only. Moving to another diagram (new, open, sample) raises
///     <see cref="DiagramReplaced"/>; every change that affects the title, the undo state or the
///     status bar raises <see cref="StateChanged"/>.
/// </remarks>
internal sealed class DemoSession
{
    /// <summary>
    ///     The name given to the first page of a new diagram.
    /// </summary>
    public const string FirstPageName = "Page 1";

    /// <summary>
    ///     The text shown when the current diagram has no file.
    /// </summary>
    public const string UntitledName = "Untitled";

    /// <summary>
    ///     The text of a freshly added annotation.
    /// </summary>
    public const string DefaultAnnotationText = "Note";

    /// <summary>
    ///     The width of a freshly added annotation, in diagram units.
    /// </summary>
    private const double AnnotationWidth = 150;

    /// <summary>
    ///     The height of a freshly added annotation, in diagram units.
    /// </summary>
    private const double AnnotationHeight = 60;

    /// <summary>
    ///     The diagram currently shown.
    /// </summary>
    private Diagram _diagram;

    /// <summary>
    ///     The last status message.
    /// </summary>
    private string _message = string.Empty;

    /// <summary>
    ///     Initializes a new instance of the <see cref="DemoSession"/> class with an empty, clean diagram.
    /// </summary>
    /// <param name="editor">The editor control that shows the current page.</param>
    /// <param name="typeSet">The node types this instance registers.</param>
    public DemoSession(DiagramEditor editor, NodeTypeSet typeSet)
    {
        Editor = editor ?? throw new ArgumentNullException(nameof(editor));
        TypeSet = typeSet;
        _diagram = CreateBlankDiagram();
        Attach(_diagram);
        Editor.Page = _diagram.Pages.FirstOrDefault();

        // Paste failures are reported once, here, whether the paste came from a key, a menu or the automation endpoint
        Editor.PasteFailed += (_, e) => SetMessage(e.Result.Message);
    }

    /// <summary>
    ///     Occurs when a different diagram replaced the current one.
    /// </summary>
    public event EventHandler? DiagramReplaced;

    /// <summary>
    ///     Occurs when the title, dirty state, undo state, page list or current page may have changed.
    /// </summary>
    public event EventHandler? StateChanged;

    /// <summary>
    ///     Occurs when the status message changed.
    /// </summary>
    public event EventHandler? MessageChanged;

    /// <summary>
    ///     Gets the editor control.
    /// </summary>
    public DiagramEditor Editor { get; }


    /// <summary>
    ///     Gets the node type set this instance registers.
    /// </summary>
    public NodeTypeSet TypeSet { get; }

    /// <summary>
    ///     Gets the diagram currently shown.
    /// </summary>
    public Diagram Diagram => _diagram;

    /// <summary>
    ///     Gets the path of the file the diagram was opened from or last saved to, or null.
    /// </summary>
    public string? FilePath { get; private set; }

    /// <summary>
    ///     Gets the page the editor shows, or null when the diagram has no pages.
    /// </summary>
    public DiagramPage? CurrentPage => Editor.Page;

    /// <summary>
    ///     Gets a value indicating whether the diagram has unsaved changes.
    /// </summary>
    public bool IsDirty => _diagram.History.IsDirty;

    /// <summary>
    ///     Gets the file name shown in the title, or <see cref="UntitledName"/>.
    /// </summary>
    public string DisplayName => FilePath is null ? UntitledName : Path.GetFileName(FilePath);

    /// <summary>
    ///     Gets the window title: <c>Plexus Demo - name</c>, followed by <c>*</c> when there are unsaved changes.
    /// </summary>
    public string Title => $"Plexus Demo - {DisplayName}{(IsDirty ? "*" : string.Empty)}";

    /// <summary>
    ///     Gets the last status message (empty when there is none).
    /// </summary>
    public string Message => _message;

    /// <summary>
    ///     Sets the status message.
    /// </summary>
    /// <param name="message">The message; null or empty clears it.</param>
    public void SetMessage(string? message)
    {
        _message = message ?? string.Empty;
        MessageChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    ///     Creates the node type registry for this instance's type set.
    /// </summary>
    /// <returns>A new registry.</returns>
    private NodeTypeRegistry CreateRegistry() =>
        TypeSet == NodeTypeSet.Basic ? DemoNodeTypes.CreateBasicRegistry() : DemoNodeTypes.CreateRegistry();

    /// <summary>
    ///     Replaces the diagram with a new empty one holding a single page.
    /// </summary>
    public void New() => Replace(CreateBlankDiagram(), null);

    /// <summary>
    ///     Replaces the diagram with one loaded from a file.
    /// </summary>
    /// <param name="path">The snapshot JSON file.</param>
    /// <exception cref="IOException">Thrown when the file cannot be read.</exception>
    /// <exception cref="System.Text.Json.JsonException">Thrown when the file is not valid JSON.</exception>
    /// <exception cref="SnapshotException">Thrown when the snapshot cannot be restored (for example a node type this instance lacks).</exception>
    public void Open(string path)
    {
        // Load completely before replacing anything, so a bad file leaves the current diagram untouched
        var loaded = DemoDocument.Load(path, CreateRegistry());
        Replace(loaded, Path.GetFullPath(path));
    }

    /// <summary>
    ///     Replaces the diagram with a built-in sample, which then has no file.
    /// </summary>
    /// <remarks>
    ///     The sample is built with the full type set and re-read through JSON using this instance's
    ///     registry, so a 'basic' instance reports the missing types instead of silently dropping them.
    /// </remarks>
    /// <param name="name">The sample name (see <see cref="DemoSamples.Names"/>).</param>
    /// <exception cref="ArgumentException">Thrown when the sample name is unknown.</exception>
    /// <exception cref="SnapshotException">Thrown when the sample uses node types this instance lacks.</exception>
    public void OpenSample(string name)
    {
        var json = DemoSamples.Create(name).ToJson();
        Replace(Diagram.FromJson(json, CreateRegistry(), DemoAppearance.Options), null);
    }

    /// <summary>
    ///     Saves the diagram to a file and marks it clean.
    /// </summary>
    /// <param name="path">The file to write; null saves to <see cref="FilePath"/>.</param>
    /// <returns>The path written.</returns>
    /// <exception cref="InvalidOperationException">Thrown when no path is given and the diagram has no file yet.</exception>
    /// <exception cref="IOException">Thrown when the file cannot be written.</exception>
    public string Save(string? path = null)
    {
        var target = path ?? FilePath ?? throw new InvalidOperationException("The diagram has no file yet; give a path.");
        target = Path.GetFullPath(target);
        DemoDocument.Save(_diagram, target);
        FilePath = target;
        _diagram.History.MarkClean();
        RaiseStateChanged();
        SetMessage($"Saved {Path.GetFileName(target)}");
        return target;
    }

    /// <summary>
    ///     Renders a page to a PNG file.
    /// </summary>
    /// <param name="page">The page to render.</param>
    /// <param name="path">The file to write.</param>
    /// <param name="scale">Pixels per diagram unit.</param>
    public static void ExportPng(DiagramPage page, string path, double scale = 1)
    {
        ArgumentNullException.ThrowIfNull(page);
        PlexusRenderer.SavePng(page, Path.GetFullPath(path), new RenderOptions { Scale = scale });
    }

    /// <summary>
    ///     Makes a page the one the editor shows.
    /// </summary>
    /// <param name="page">A page of the current diagram.</param>
    public void SelectPage(DiagramPage page)
    {
        ArgumentNullException.ThrowIfNull(page);
        if (!page.IsAttached || !ReferenceEquals(page.Diagram, _diagram))
        {
            throw new InvalidOperationException("The page does not belong to the current diagram.");
        }

        Editor.Page = page;
        RaiseStateChanged();
    }

    /// <summary>
    ///     Adds a page after the last one and shows it.
    /// </summary>
    /// <param name="name">The page name; null picks "Page n".</param>
    /// <returns>The new page.</returns>
    public DiagramPage AddPage(string? name = null)
    {
        var page = _diagram.Pages.Add(string.IsNullOrWhiteSpace(name) ? NextPageName() : name.Trim());
        SelectPage(page);
        return page;
    }

    /// <summary>
    ///     Deletes a page (undoable). The last remaining page cannot be deleted.
    /// </summary>
    /// <param name="page">The page to delete.</param>
    /// <exception cref="InvalidOperationException">Thrown when it is the only page.</exception>
    public void DeletePage(DiagramPage page)
    {
        ArgumentNullException.ThrowIfNull(page);
        if (_diagram.Pages.Count <= 1)
        {
            throw new InvalidOperationException("A diagram needs at least one page.");
        }

        _diagram.Pages.Remove(page);
    }

    /// <summary>
    ///     Moves a page one place left or right (undoable).
    /// </summary>
    /// <param name="page">The page to move.</param>
    /// <param name="delta">-1 for left, +1 for right.</param>
    public void MovePage(DiagramPage page, int delta)
    {
        ArgumentNullException.ThrowIfNull(page);
        var target = _diagram.Pages.IndexOf(page) + delta;
        if (target >= 0 && target < _diagram.Pages.Count)
        {
            _diagram.Pages.Move(page, target);
        }
    }

    /// <summary>
    ///     Adds a node of a registered type and selects it.
    /// </summary>
    /// <param name="typeKey">The node type key.</param>
    /// <param name="at">The top-left corner in diagram coordinates.</param>
    /// <param name="name">The node name; null generates one.</param>
    /// <returns>The new node.</returns>
    public Node AddNode(string typeKey, PlexusPoint at, string? name = null)
    {
        var node = RequirePage().AddNode(typeKey, at.X, at.Y, name);
        Editor.Select(node);
        return node;
    }

    /// <summary>
    ///     Flips the <c>On</c> property of a switch node, which recolors the logic wires it drives.
    /// </summary>
    /// <remarks>
    ///     This is an ordinary undoable property edit; the wire styler reads the new value the next
    ///     time the scene is built, so nothing else needs to be told.
    /// </remarks>
    /// <param name="node">The switch node.</param>
    /// <returns>The new state of the switch.</returns>
    /// <exception cref="ArgumentException">Thrown when the node is not a switch.</exception>
    public static bool ToggleSwitch(Node node)
    {
        ArgumentNullException.ThrowIfNull(node);
        if (node.Type.Key != DemoNodeTypes.Switch)
        {
            throw new ArgumentException($"'{node.Name}' is not a switch.", nameof(node));
        }

        var on = !node.Properties.Get<bool>("On");
        node.Properties["On"] = on;
        return on;
    }

    /// <summary>
    ///     Adds an annotation and selects it.
    /// </summary>
    /// <param name="at">The top-left corner in diagram coordinates.</param>
    /// <param name="text">The text; null uses <see cref="DefaultAnnotationText"/>.</param>
    /// <returns>The new annotation.</returns>
    public Annotation AddAnnotation(PlexusPoint at, string? text = null)
    {
        var annotation = RequirePage().AddAnnotation(text ?? DefaultAnnotationText, new PlexusRect(at.X, at.Y, AnnotationWidth, AnnotationHeight));
        Editor.Select(annotation);
        return annotation;
    }

    /// <summary>
    ///     Finds a page by zero-based index or by name.
    /// </summary>
    /// <param name="selector">An index (digits) or a page name; a name wins when a page is named like a number.</param>
    /// <returns>The page.</returns>
    /// <exception cref="ArgumentException">Thrown when no page matches.</exception>
    public DiagramPage FindPage(string selector)
    {
        ArgumentNullException.ThrowIfNull(selector);
        var byName = _diagram.Pages.FirstOrDefault(p => string.Equals(p.Name, selector, StringComparison.Ordinal));
        if (byName is not null)
        {
            return byName;
        }

        if (int.TryParse(selector, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var index) && index < _diagram.Pages.Count)
        {
            return _diagram.Pages[index];
        }

        throw new ArgumentException($"There is no page '{selector}'.");
    }

    /// <summary>
    ///     Gets the shown page or fails with a friendly message.
    /// </summary>
    /// <returns>The page.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the diagram has no page.</exception>
    public DiagramPage RequirePage() => CurrentPage is { IsAttached: true } page ? page : throw new InvalidOperationException("The diagram has no page.");

    /// <summary>
    ///     Creates a blank diagram with one page and an empty undo history.
    /// </summary>
    /// <returns>The diagram.</returns>
    private Diagram CreateBlankDiagram()
    {
        // The first page is not an undoable edit; ending the suspension leaves the new document clean
        var diagram = new Diagram(CreateRegistry(), DemoAppearance.Options);
        using (diagram.History.Suspend())
        {
            diagram.Pages.Add(FirstPageName);
        }

        return diagram;
    }

    /// <summary>
    ///     Switches to another diagram and file.
    /// </summary>
    /// <param name="diagram">The new diagram.</param>
    /// <param name="path">The file it came from, or null.</param>
    private void Replace(Diagram diagram, string? path)
    {
        Detach(_diagram);
        Editor.ClearSelection();
        _diagram = diagram;
        FilePath = path;
        Attach(diagram);
        Editor.Page = diagram.Pages.FirstOrDefault();
        DiagramReplaced?.Invoke(this, EventArgs.Empty);
        RaiseStateChanged();
    }

    /// <summary>
    ///     Starts listening to a diagram.
    /// </summary>
    /// <param name="diagram">The diagram.</param>
    private void Attach(Diagram diagram)
    {
        diagram.History.Changed += OnHistoryChanged;
        diagram.Pages.CollectionChanged += OnPagesChanged;
    }

    /// <summary>
    ///     Stops listening to a diagram.
    /// </summary>
    /// <param name="diagram">The diagram.</param>
    private void Detach(Diagram diagram)
    {
        diagram.History.Changed -= OnHistoryChanged;
        diagram.Pages.CollectionChanged -= OnPagesChanged;
    }

    /// <summary>
    ///     Reports undo/redo/dirty changes.
    /// </summary>
    /// <param name="sender">The history.</param>
    /// <param name="e">Unused.</param>
    private void OnHistoryChanged(object? sender, EventArgs e) => RaiseStateChanged();

    /// <summary>
    ///     Reports state changes when pages come and go (including through undo and redo); the editor itself switches to the neighbor page when the shown one is removed.
    /// </summary>
    /// <param name="sender">The page collection.</param>
    /// <param name="e">What changed.</param>
    private void OnPagesChanged(object? sender, NotifyCollectionChangedEventArgs e) => RaiseStateChanged();

    /// <summary>
    ///     Raises <see cref="StateChanged"/>.
    /// </summary>
    private void RaiseStateChanged() => StateChanged?.Invoke(this, EventArgs.Empty);

    /// <summary>
    ///     Picks the first free page name of the form "Page n".
    /// </summary>
    /// <returns>The name.</returns>
    private string NextPageName()
    {
        var n = _diagram.Pages.Count + 1;
        while (_diagram.Pages.Any(p => p.Name == $"Page {n}"))
        {
            n++;
        }

        return $"Page {n}";
    }
}
