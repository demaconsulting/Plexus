using System.ComponentModel;

namespace DemaConsulting.Plexus.Avalonia.PropertyGrid;

/// <content>
///     Nested properties of expandable objects and the elements of collection properties.
/// </content>
public abstract partial class PropertyItem
{
    /// <summary>
    ///     The cached children and the value / type they were built for.
    /// </summary>
    private IReadOnlyList<PropertyItem>? _childItems;

    /// <summary>
    ///     The value instance (reference types) the cached children were built for.
    /// </summary>
    private object? _childItemsValue;

    /// <summary>
    ///     Gets a value indicating whether the property expands to show the properties of its value: its type
    ///     converter reports that it supports properties, as <see cref="ExpandableObjectConverter"/> does (applied
    ///     with <c>[TypeConverter(typeof(ExpandableObjectConverter))]</c> on the property or on its type).
    /// </summary>
    public bool IsExpandable
    {
        get
        {
            try
            {
                TryGetValue(out var value);
                return GetEffectiveConverter(value).GetPropertiesSupported(new PropertyContext(this));
            }
            catch (Exception ex) when (!PropertyErrors.IsFatal(ex))
            {
                return false;
            }
        }
    }

    /// <summary>
    ///     Gets the browsable properties of the current value of an expandable property. The list is empty when the
    ///     property is not expandable, when its value is <see langword="null"/> or when reading it fails. The same
    ///     item instances are returned while the value stays the same instance (value types: the same type).
    /// </summary>
    /// <remarks>
    ///     Child items read and write through this item, so editing a child of a value-type (struct) property writes the
    ///     modified struct back to this item's property. A read-only value-type property has read-only children.
    /// </remarks>
    public IReadOnlyList<PropertyItem> Children
    {
        get
        {
            try
            {
                return BuildChildren();
            }
            catch (Exception ex) when (!PropertyErrors.IsFatal(ex))
            {
                _childItems = null;
                return [];
            }
        }
    }

    /// <summary>
    ///     Builds or reuses the child items.
    /// </summary>
    /// <returns>The children.</returns>
    private IReadOnlyList<PropertyItem> BuildChildren()
    {
        if (!TryGetValue(out var value).Succeeded || value is null)
        {
            _childItems = null;
            _childItemsValue = null;
            return [];
        }

        var reusable = _childItems is not null &&
                       (value.GetType().IsValueType ? _childItemsValue?.GetType() == value.GetType() : ReferenceEquals(_childItemsValue, value));
        if (reusable)
        {
            return _childItems!;
        }

        var converter = GetEffectiveConverter(value);
        var context = new PropertyContext(this);
        var children = new List<PropertyItem>();
        if (converter.GetPropertiesSupported(context))
        {
            var properties = converter.GetProperties(context, value, [BrowsableAttribute.Yes]);
            if (properties is not null)
            {
                foreach (PropertyDescriptor descriptor in properties)
                {
                    children.Add(new ReflectionPropertyItem(descriptor, () => TryGetValue(out var owner).Succeeded ? owner : null, this));
                }
            }
        }

        _childItems = children;
        _childItemsValue = value;
        return children;
    }

    /// <summary>
    ///     Gets a value indicating whether the property is a collection the grid can show and edit: an array, an
    ///     <see cref="System.Collections.IList"/> (including <see cref="List{T}"/>), an <c>ICollection&lt;T&gt;</c> or
    ///     an <c>IReadOnlyCollection&lt;T&gt;</c>. Strings and dictionaries are not collections.
    /// </summary>
    public bool IsCollection => CollectionTypes.TryGetElementType(PropertyType, out _);

    /// <summary>
    ///     Gets the element type of a collection property, or <see langword="null"/> when the property is not a
    ///     collection. Non-generic <see cref="System.Collections.IList"/> collections have <see cref="object"/> elements.
    /// </summary>
    public Type? CollectionItemType => CollectionTypes.TryGetElementType(PropertyType, out var elementType) ? elementType : null;

    /// <summary>
    ///     Takes a snapshot of the elements of a collection property.
    /// </summary>
    /// <returns>The elements in order; empty when the property is not a collection, is <see langword="null"/> or cannot be read.</returns>
    public IReadOnlyList<object?> GetCollectionValues() =>
        IsCollection && TryGetValue(out var value).Succeeded ? CollectionTypes.Snapshot(value) : [];

    /// <summary>
    ///     Gets one item per element of a collection property. Each item reads and writes its element through this
    ///     item, so an element that is itself an expandable object exposes its properties through
    ///     <see cref="Children"/>, and the element paths look like <c>Tags[2]</c>.
    /// </summary>
    /// <returns>The element items in order; empty when the property is not a collection or is <see langword="null"/>.</returns>
    public IReadOnlyList<PropertyItem> GetCollectionItems()
    {
        if (CollectionItemType is not { } elementType)
        {
            return [];
        }

        var count = GetCollectionValues().Count;
        var result = new List<PropertyItem>(count);
        for (var index = 0; index < count; index++)
        {
            result.Add(new CollectionElementItem(this, index, elementType));
        }

        return result;
    }
}
