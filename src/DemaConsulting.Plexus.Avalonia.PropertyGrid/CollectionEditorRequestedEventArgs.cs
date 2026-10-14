namespace DemaConsulting.Plexus.Avalonia.PropertyGrid;

/// <summary>
///     Describes a request to edit a collection property, raised by a <see cref="PropertyGrid"/> before it opens its
///     own collection editor so a host can show a different editor.
/// </summary>
public sealed class CollectionEditorRequestedEventArgs : EventArgs
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="CollectionEditorRequestedEventArgs"/> class.
    /// </summary>
    /// <param name="session">The grid's edit session.</param>
    /// <param name="item">The collection property.</param>
    internal CollectionEditorRequestedEventArgs(PropertyEditSession session, PropertyItem item)
    {
        Session = session;
        Item = item;
    }

    /// <summary>
    ///     Gets the edit session of the grid; a replacement editor applies its result through
    ///     <see cref="PropertyEditSession.BeginCollectionEdit"/>.
    /// </summary>
    public PropertyEditSession Session { get; }

    /// <summary>
    ///     Gets the collection property to edit.
    /// </summary>
    public PropertyItem Item { get; }

    /// <summary>
    ///     Gets or sets a value indicating whether a handler took over, so the grid does not open its own editor.
    /// </summary>
    public bool Handled { get; set; }
}
