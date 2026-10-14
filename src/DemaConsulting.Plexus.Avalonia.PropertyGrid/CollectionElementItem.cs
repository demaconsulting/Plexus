using System.Collections;

namespace DemaConsulting.Plexus.Avalonia.PropertyGrid;

/// <summary>
///     The item for one element of a collection property.
/// </summary>
internal sealed class CollectionElementItem : PropertyItem
{
    /// <summary>
    ///     The collection property.
    /// </summary>
    private readonly PropertyItem _collection;

    /// <summary>
    ///     The element index.
    /// </summary>
    private readonly int _index;

    /// <summary>
    ///     The element type.
    /// </summary>
    private readonly Type _elementType;

    /// <summary>
    ///     Initializes a new instance of the <see cref="CollectionElementItem"/> class.
    /// </summary>
    /// <param name="collection">The collection property.</param>
    /// <param name="index">The element index.</param>
    /// <param name="elementType">The element type.</param>
    internal CollectionElementItem(PropertyItem collection, int index, Type elementType)
        : base(collection)
    {
        _collection = collection;
        _index = index;
        _elementType = elementType;
    }

    /// <inheritdoc />
    public override string Name => $"[{_index}]";

    /// <inheritdoc />
    public override Type PropertyType => _elementType;

    /// <inheritdoc />
    public override string Category => _collection.Category;

    /// <inheritdoc />
    public override bool IsReadOnly => GetList() is not { IsReadOnly: false };

    /// <inheritdoc />
    internal override bool IsIndexer => true;

    /// <inheritdoc />
    public override object? GetValue()
    {
        var list = GetList();
        if (list is not null && _index < list.Count)
        {
            return list[_index];
        }

        var snapshot = _collection.GetCollectionValues();
        return _index < snapshot.Count
            ? snapshot[_index]
            : throw new InvalidOperationException($"The collection no longer has an element at index {_index}.");
    }

    /// <inheritdoc />
    public override void SetValue(object? value)
    {
        if (GetList() is not { IsReadOnly: false } list)
        {
            throw new NotSupportedException("The elements of this collection cannot be replaced in place.");
        }

        list[_index] = value;
    }

    /// <summary>
    ///     Gets the collection as a non-generic list, when it is one.
    /// </summary>
    /// <returns>The list, or <see langword="null"/>.</returns>
    private IList? GetList() => _collection.TryGetValue(out var value).Succeeded ? value as IList : null;
}
