namespace DemaConsulting.Plexus;

/// <summary>
///     The portable value kinds a node property can hold.
/// </summary>
/// <remarks>
///     The set is intentionally small and JSON-safe so snapshots and the clipboard can round-trip
///     every property without type-name resolution. A UI that edits properties maps the kinds to controls: <see cref="Boolean"/> to a
///     check box; <see cref="Int32"/>, <see cref="Int64"/> and <see cref="Double"/> to a numeric or text box; <see cref="String"/> to a
///     text box; <see cref="Choice"/> to a combo box fed from <see cref="PropertyDefinition.Choices"/>; <see cref="Color"/> to a color
///     picker. Turning the user's text into a value, and the reason when it does not fit, is
///     <see cref="PropertyDefinition.TryParse"/>; applying all edits at once is <see cref="PropertyBag.TrySetAll"/>.
/// </remarks>
/// <example>
///     Apply the texts of a properties dialog as ONE undo entry, or show why not.
///     <code>
///     using DemaConsulting.Plexus;
///
///     var diagram = new Diagram(types);                     // types: the registry of the DemaConsulting.Plexus example
///     var node = diagram.Pages.Add("Main").AddNode("demo.Source", 0, 0, name: "A");
///
///     // texts: what the dialog's edit boxes hold, by property name
///     var dialogTexts = new Dictionary&lt;string, string&gt; { ["Value"] = "abc" };
///     var texts = node.Type.Properties.Where(p =&gt; !p.IsReadOnly &amp;&amp; dialogTexts.ContainsKey(p.Name))
///         .ToDictionary(p =&gt; p.Name, p =&gt; (object?)dialogTexts[p.Name]);
///
///     if (!node.Properties.TrySetAll(texts, out var errors))
///     {
///         foreach (var (name, message) in errors)
///         {
///             Console.WriteLine($"{name}: {message}");       // e.g. "Value: 'abc' is not a number." Nothing was changed.
///         }
///     }
///
///     string shown = PropertyDefinition.FormatForEdit(node.Properties["Value"]);   // the text to put into the edit box
///     </code>
/// </example>
public enum PropertyValueKind
{
    /// <summary>A <see cref="bool"/> value.</summary>
    Boolean,

    /// <summary>A 32-bit signed integer.</summary>
    Int32,

    /// <summary>A 64-bit signed integer.</summary>
    Int64,

    /// <summary>A finite double-precision number.</summary>
    Double,

    /// <summary>Free text.</summary>
    String,

    /// <summary>A string constrained to a fixed list of options.</summary>
    Choice,

    /// <summary>A Plexus <see cref="DemaConsulting.Plexus.Geometry.Color"/> (an ARGB color, written as <c>"#AARRGGBB"</c> in JSON).</summary>
    Color,
}
