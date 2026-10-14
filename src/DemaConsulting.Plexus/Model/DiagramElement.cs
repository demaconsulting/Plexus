using System.ComponentModel;
using DemaConsulting.Plexus.Scene;

namespace DemaConsulting.Plexus;

/// <summary>
///     Base class of everything placed on a <see cref="DiagramPage"/>: nodes, wires, annotations and groups.
/// </summary>
/// <remarks>
///     Elements are created through the page API (for example <see cref="DiagramPage.AddNode(string, double, double, string?, string?)"/>),
///     never directly, so the base class has no public constructor. An element that has been removed
///     has a null <see cref="Page"/> and cannot be mutated until an undo re-attaches it. Elements
///     raise <see cref="PropertyChanged"/> for every changed property, including during undo and redo.
///     Not thread-safe.
/// </remarks>
public abstract class DiagramElement : INotifyPropertyChanged
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="DiagramElement"/> class.
    /// </summary>
    /// <param name="id">The element id.</param>
    private protected DiagramElement(string id) => Id = id;

    /// <summary>
    ///     Occurs after one of the element's properties (for example <c>Name</c>, <c>Bounds</c>, <c>Route</c>, <c>IsCollapsed</c>) changed.
    /// </summary>
    /// <remarks>
    ///     The event is raised for changes made through the API and for changes made by undo and redo, on the
    ///     thread that owns the diagram. <see cref="System.ComponentModel.PropertyChangedEventArgs.PropertyName"/>
    ///     names the property. Use <see cref="Diagram.Changed"/> instead to observe the whole diagram with one handler.
    /// </remarks>
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>
    ///     Gets the element's identifier, stable across undo and redo.
    /// </summary>
    public string Id { get; }

    /// <summary>
    ///     Gets the page the element is on, or null while it is removed.
    /// </summary>
    public DiagramPage? Page { get; internal set; }

    /// <summary>
    ///     Gets the group the element belongs to, or null when it is not grouped.
    /// </summary>
    public Group? Parent { get; internal set; }

    /// <summary>
    ///     Gets the theme in effect for this element, falling back to the classic theme when detached.
    /// </summary>
    internal DiagramTheme Theme => Page?.Diagram.Theme ?? DiagramTheme.Classic;

    /// <summary>
    ///     Gets a value indicating whether the element is shown: false when it sits inside a collapsed group.
    /// </summary>
    /// <remarks>
    ///     Collapsed groups hide their members (the group itself stays visible as a box). The check
    ///     walks all ancestor groups, so an element nested several levels deep is hidden when any
    ///     level is collapsed. A wire is reported visible by this property regardless of its
    ///     endpoints; a wire is drawn only while at least one of its nodes is visible.
    /// </remarks>
    public bool IsVisible => !IsHiddenByCollapse;

    /// <summary>
    ///     Gets the rectangle the element occupies on the page, in diagram coordinates.
    /// </summary>
    /// <remarks>
    ///     Nodes, annotations and groups return their <c>Bounds</c> (for a collapsed group, the
    ///     collapsed box). A wire returns the bounding box of its full polyline, source anchor and
    ///     target anchor included. Labels and arrowheads drawn outside these rectangles are not counted.
    /// </remarks>
    /// <returns>The bounding rectangle.</returns>
    public abstract Rect GetBounds();

    /// <summary>
    ///     Gets a value indicating whether an ancestor group is collapsed, hiding this element.
    /// </summary>
    internal bool IsHiddenByCollapse
    {
        get
        {
            for (var group = Parent; group is not null; group = group.Parent)
            {
                if (group.IsCollapsed)
                {
                    return true;
                }
            }

            return false;
        }
    }

    /// <summary>
    ///     Removes this element from its page (undoable); equivalent to <see cref="DiagramPage.Remove(DiagramElement)"/>.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when the element is not on a page.</exception>
    public void Remove() => RequirePage().Remove(this);

    /// <summary>
    ///     Gets the page the element is on, requiring that the page is attached to a diagram.
    /// </summary>
    /// <returns>The attached page.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the element was removed or its page was removed.</exception>
    internal DiagramPage RequirePage()
    {
        if (Page is { IsAttached: true } page)
        {
            return page;
        }

        throw new InvalidOperationException("The element is not attached to a diagram; undo the removal before modifying it.");
    }

    /// <summary>
    ///     Raises <see cref="PropertyChanged"/> and reports the change to the owning diagram.
    /// </summary>
    /// <param name="propertyName">The changed property.</param>
    internal void NotifyChanged(string propertyName)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        Page?.Diagram.RaiseChanged(DiagramChangeKind.ElementChanged, Page, this, propertyName);
    }
}
