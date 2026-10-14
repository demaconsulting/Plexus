using System.Diagnostics.CodeAnalysis;

namespace DemaConsulting.Plexus;

/// <summary>
///     A free-floating text note on a page, drawn as a resizable box.
/// </summary>
/// <remarks>
///     Create annotations with <see cref="DiagramPage.AddAnnotation"/>. Every setter is undoable and throws
///     <see cref="InvalidOperationException"/> while the annotation is removed from its page.
///     Annotations can be members of a <see cref="Group"/>. In the Avalonia editor a selected annotation shows a resize
///     handle in its corner and double-clicking it (or pressing F2 with it selected) edits the text in place.
///     Not thread-safe.
///     <para>
///         Inherited from <see cref="DiagramElement"/> (the member tables of this page list only what <see cref="Annotation"/> declares): <see cref="DiagramElement.Id"/>,
///         <see cref="DiagramElement.Page"/> (null while the annotation is removed), <see cref="DiagramElement.Parent"/> (the group it belongs to, or null),
///         <see cref="DiagramElement.IsVisible"/>, <see cref="DiagramElement.GetBounds"/> (the same rectangle as <see cref="Bounds"/>), <see cref="DiagramElement.Remove"/>
///         and the <see cref="DiagramElement.PropertyChanged"/> event.
///     </para>
/// </remarks>
/// <example>
///     Add a note, edit it and recolor it.
///     <code>
///     using DemaConsulting.Plexus;
///     using DemaConsulting.Plexus.Geometry;
///
///     var diagram = new Diagram(types);                    // types: the registry of the DemaConsulting.Plexus example
///     var page = diagram.Pages.Add("Main");
///
///     Annotation note = page.AddAnnotation("Check the wiring", new Rect(0, -80, 160, 50));
///     note.Text = "Wiring checked";                         // undoable
///     note.Bounds = new Rect(0, -80, 220, 50);              // resize (a different size) or move (the same size)
///     note.Fill = Color.Parse("#FFF4C2");                   // null (the default) uses the theme's annotation fill
///     </code>
/// </example>
public sealed partial class Annotation : DiagramElement
{
    /// <summary>
    ///     The note text.
    /// </summary>
    private string _text;

    /// <summary>
    ///     The bounding box.
    /// </summary>
    private Rect _bounds;

    /// <summary>
    ///     The optional fill override.
    /// </summary>
    private Color? _fill;

    /// <summary>
    ///     Initializes a new instance of the <see cref="Annotation"/> class.
    /// </summary>
    /// <param name="id">The element id.</param>
    /// <param name="text">The note text.</param>
    /// <param name="bounds">The bounding box.</param>
    internal Annotation(string id, string text, Rect bounds)
        : base(id)
    {
        _text = text;
        _bounds = bounds;
    }

    /// <summary>
    ///     Gets or sets the note text (undoable).
    /// </summary>
    /// <exception cref="ArgumentNullException">Thrown when set to null.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the annotation is removed from its page.</exception>
    public string Text
    {
        get => _text;
        set
        {
            ArgumentNullException.ThrowIfNull(value);
            var page = RequirePage();
            if (string.Equals(_text, value, StringComparison.Ordinal))
            {
                return;
            }

            page.Diagram.History.Execute(new SetValueCommand<string>(
                "Edit annotation text",
                this,
                nameof(Text),
                _text,
                value,
                v =>
                {
                    _text = v;
                    NotifyChanged(nameof(Text));
                }));
        }
    }

    /// <summary>
    ///     Gets or sets the bounding box (undoable); annotations are resizable.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when the annotation is removed from its page.</exception>
    [SuppressMessage("Major Code Smell", "S4275:Getters and setters should access the expected fields", Justification = "The setter routes through the undoable layout pipeline, which writes the field via Annotation.SetBoundsCore.")]
    public Rect Bounds
    {
        get => _bounds;
        set => RequirePage().ApplyBounds(this, value);
    }

    /// <summary>
    ///     Gets the rectangle the note occupies (the same value as <see cref="Bounds"/>).
    /// </summary>
    /// <returns>The note's bounding box in diagram coordinates.</returns>
    public override Rect GetBounds() => _bounds;

    /// <summary>
    ///     Gets or sets the fill color (undoable); null selects the theme's annotation fill.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when the annotation is removed from its page.</exception>
    public Color? Fill
    {
        get => _fill;
        set
        {
            var page = RequirePage();
            if (_fill == value)
            {
                return;
            }

            page.Diagram.History.Execute(new SetValueCommand<Color?>(
                "Change annotation fill",
                this,
                nameof(Fill),
                _fill,
                value,
                v =>
                {
                    _fill = v;
                    NotifyChanged(nameof(Fill));
                }));
        }
    }

    /// <summary>
    ///     Stores the bounds and raises change notifications; used by commands.
    /// </summary>
    /// <param name="bounds">The bounds to store.</param>
    internal void SetBoundsCore(Rect bounds)
    {
        _bounds = bounds;
        NotifyChanged(nameof(Bounds));
    }
}
