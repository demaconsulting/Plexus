namespace DemaConsulting.Plexus.Avalonia.PropertyGrid;

/// <summary>
///     What a <see cref="PendingEdit"/> will do to the target when committed.
/// </summary>
public enum PendingEditKind
{
    /// <summary>
    ///     Assign a new value to the property.
    /// </summary>
    Value,

    /// <summary>
    ///     Restore the property's default value.
    /// </summary>
    Reset,

    /// <summary>
    ///     Replace the contents of a collection property with the working copy of a <see cref="CollectionEditor"/>.
    /// </summary>
    Collection,
}

/// <summary>
///     One buffered edit of a staged <see cref="PropertyEditSession"/>.
/// </summary>
public sealed class PendingEdit
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="PendingEdit"/> class.
    /// </summary>
    /// <param name="item">The edited item.</param>
    /// <param name="kind">The kind of edit.</param>
    /// <param name="value">The new value (<see cref="PendingEditKind.Value"/> only).</param>
    /// <param name="collection">The collection editor (<see cref="PendingEditKind.Collection"/> only).</param>
    internal PendingEdit(PropertyItem item, PendingEditKind kind, object? value, CollectionEditor? collection)
    {
        Item = item;
        Kind = kind;
        Value = value;
        Collection = collection;
    }

    /// <summary>
    ///     Gets the edited item.
    /// </summary>
    public PropertyItem Item { get; }

    /// <summary>
    ///     Gets the path of the edited item (the key of the edit within the session).
    /// </summary>
    public string Path => Item.Path;

    /// <summary>
    ///     Gets what the edit does.
    /// </summary>
    public PendingEditKind Kind { get; internal set; }

    /// <summary>
    ///     Gets the value that will be assigned (<see cref="PendingEditKind.Value"/>); otherwise <see langword="null"/>.
    /// </summary>
    public object? Value { get; internal set; }

    /// <summary>
    ///     Gets the collection editor holding the working copy (<see cref="PendingEditKind.Collection"/>); otherwise
    ///     <see langword="null"/>.
    /// </summary>
    public CollectionEditor? Collection { get; internal set; }

    /// <summary>
    ///     Gets the error from the last attempt to commit the edit, or <see langword="null"/>.
    /// </summary>
    public string? Error { get; internal set; }
}
