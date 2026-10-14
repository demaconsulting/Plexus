using AvaloniaColor = Avalonia.Media.Color;
using PlexusColor = DemaConsulting.Plexus.Geometry.Color;

namespace DemaConsulting.Plexus.Avalonia.PropertyGrid.Plexus;

/// <summary>
///     Shows and edits one Plexus <see cref="Annotation"/> in a property grid: its text and its fill color.
///     <c>grid.SelectedObject = annotation</c> uses it automatically.
/// </summary>
/// <remarks>
///     The fill is shown as an <c>Avalonia.Media.Color</c> that may be empty; empty selects the theme's annotation fill
///     (the value is bold when a fill is set, and reset clears it). Like <see cref="NodePropertySource"/>, the source is an
///     <see cref="IPropertyCommitScope"/>: each commit is one undo entry whatever the grid's
///     <see cref="PropertyEditMode"/>, and the source raises <see cref="Changed"/> when the annotation changes from
///     elsewhere. Dispose the source to detach from the annotation.
/// </remarks>
/// <example>
///     <code><![CDATA[
///     var grid = new PropertyGrid { EditMode = PropertyEditMode.Live, SelectedObject = annotation };
///     ]]></code>
/// </example>
public sealed class AnnotationPropertySource : IPropertySource, IPropertyCommitScope, IDisposable
{
    /// <summary>
    ///     The edited annotation.
    /// </summary>
    private readonly Annotation _annotation;

    /// <summary>
    ///     The commit plumbing.
    /// </summary>
    private readonly ElementEditing _editing;

    /// <summary>
    ///     The items.
    /// </summary>
    private readonly List<PropertyItem> _items;

    /// <summary>
    ///     Initializes a new instance of the <see cref="AnnotationPropertySource"/> class.
    /// </summary>
    /// <param name="annotation">The annotation to show and edit; it must be attached to a diagram.</param>
    /// <exception cref="ArgumentNullException"><paramref name="annotation"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="annotation"/> is not attached to a diagram.</exception>
    public AnnotationPropertySource(Annotation annotation)
    {
        ArgumentNullException.ThrowIfNull(annotation);
        _annotation = annotation;
        _editing = new ElementEditing(annotation, [nameof(Annotation.Text), nameof(Annotation.Fill)], Publish, () => { });
        var work = _editing.Work;
        _items =
        [
            new ElementPropertyItem(
                nameof(Annotation.Text),
                typeof(string),
                work,
                () => _annotation.Text,
                new CustomPropertyOptions { Category = "General", Description = "The note text shown on the diagram." },
                value => value is null ? "The text cannot be empty." : null),
            new ElementPropertyItem(
                nameof(Annotation.Fill),
                typeof(AvaloniaColor?),
                work,
                () => _annotation.Fill is { } fill ? AvaloniaColor.FromArgb(fill.A, fill.R, fill.G, fill.B) : null,
                new CustomPropertyOptions
                {
                    Category = "General",
                    Description = "The background color of the note; empty uses the theme's annotation fill.",
                    HasDefault = true,
                    DefaultValue = null,
                }),
        ];
        _editing.Changed += (_, e) => Changed?.Invoke(this, e);
    }

    /// <inheritdoc />
    public event EventHandler<PropertySourceChangedEventArgs>? Changed;

    /// <summary>
    ///     Gets the annotation that is shown and edited.
    /// </summary>
    public Annotation Annotation => _annotation;

    /// <inheritdoc />
    public IReadOnlyList<PropertyItem> GetItems() => _items;

    /// <inheritdoc />
    public void RunCommit(Action apply) => _editing.RunCommit(apply);

    /// <summary>
    ///     Stops listening to the annotation.
    /// </summary>
    public void Dispose() => _editing.Dispose();

    /// <summary>
    ///     Copies the written values to the annotation (inside the commit's undo transaction).
    /// </summary>
    private void Publish()
    {
        if (_editing.Work.TryGet(nameof(Annotation.Text), out var text) && text is string value)
        {
            _annotation.Text = value;
        }

        if (_editing.Work.TryGet(nameof(Annotation.Fill), out var fill))
        {
            _annotation.Fill = fill is AvaloniaColor color ? PlexusColor.FromArgb(color.A, color.R, color.G, color.B) : null;
        }
    }
}
