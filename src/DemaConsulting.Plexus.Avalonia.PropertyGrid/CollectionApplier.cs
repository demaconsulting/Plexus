using System.Collections;
using System.Reflection;

namespace DemaConsulting.Plexus.Avalonia.PropertyGrid;

/// <summary>
///     Writes a working copy of a collection back into a collection property, and creates new collection elements.
/// </summary>
internal static class CollectionApplier
{
    /// <summary>
    ///     Works out what can be done to the collection behind an item.
    /// </summary>
    /// <param name="item">The collection property.</param>
    /// <param name="canResize">Whether elements can be added or removed.</param>
    /// <param name="canReorder">Whether elements can be reordered or replaced.</param>
    internal static void GetCapabilities(PropertyItem item, out bool canResize, out bool canReorder)
    {
        var replaceable = !item.IsReadOnly;
        if (!item.TryGetValue(out var current).Succeeded || current is null)
        {
            canResize = replaceable;
            canReorder = replaceable;
            return;
        }

        switch (current)
        {
            case Array:
                canResize = replaceable;
                canReorder = true;
                break;
            case IList list:
                canResize = (!list.IsReadOnly && !list.IsFixedSize) || replaceable;
                canReorder = !list.IsReadOnly || replaceable;
                break;
            default:
                var generic = CollectionTypes.GetGenericCollectionInterface(current, item.CollectionItemType ?? typeof(object));
                var writable = generic is not null && !IsReadOnlyGeneric(generic, current);
                canResize = writable || replaceable;
                canReorder = writable || replaceable;
                break;
        }
    }

    /// <summary>
    ///     Writes the working copy of a collection into the collection property.
    /// </summary>
    /// <param name="item">The collection property.</param>
    /// <param name="working">The elements the collection must contain afterwards, in order.</param>
    /// <remarks>
    ///     Arrays of the same length and writable lists of the same length are updated in place; resizable lists and
    ///     <c>ICollection&lt;T&gt;</c> instances are cleared and refilled in place (so a get-only collection property
    ///     keeps its instance); otherwise a new collection is built and assigned through the property setter.
    /// </remarks>
    /// <exception cref="NotSupportedException">The collection is read-only or its type cannot be created.</exception>
    internal static void Apply(PropertyItem item, IReadOnlyList<object?> working)
    {
        var elementType = item.CollectionItemType ?? typeof(object);
        item.TryGetValue(out var current);

        if (TryApplyInPlace(current, elementType, working))
        {
            return;
        }

        if (item.IsReadOnly)
        {
            throw new NotSupportedException(
                current is Array
                    ? $"'{item.DisplayName}' is a fixed-size array behind a read-only property; its length cannot change."
                    : $"'{item.DisplayName}' is read-only and its collection cannot be modified.");
        }

        item.SetValue(CreateCollection(item.PropertyType, elementType, working));
    }

    /// <summary>
    ///     Tries to update the current collection instance in place.
    /// </summary>
    /// <param name="current">The current collection, or null.</param>
    /// <param name="elementType">The element type.</param>
    /// <param name="working">The elements the collection must contain afterwards.</param>
    /// <returns><see langword="true"/> when the instance was updated.</returns>
    private static bool TryApplyInPlace(object? current, Type elementType, IReadOnlyList<object?> working)
    {
        if (current is Array array)
        {
            if (array.Length != working.Count)
            {
                return false;
            }

            for (var i = 0; i < working.Count; i++)
            {
                array.SetValue(working[i], i);
            }

            return true;
        }

        if (current is IList list)
        {
            return TryApplyToList(list, working);
        }

        if (current is not null && CollectionTypes.GetGenericCollectionInterface(current, elementType) is { } generic &&
            !IsReadOnlyGeneric(generic, current))
        {
            generic.GetMethod(nameof(ICollection<>.Clear))!.Invoke(current, null);
            var add = generic.GetMethod(nameof(ICollection<>.Add))!;
            foreach (var element in working)
            {
                add.Invoke(current, [element]);
            }

            return true;
        }

        return false;
    }

    /// <summary>
    ///     Tries to refill a resizable list, or overwrite a writable list of the same length, in place.
    /// </summary>
    /// <param name="list">The list.</param>
    /// <param name="working">The elements the list must contain afterwards.</param>
    /// <returns><see langword="true"/> when the list was updated.</returns>
    private static bool TryApplyToList(IList list, IReadOnlyList<object?> working)
    {
        if (!list.IsReadOnly && !list.IsFixedSize)
        {
            list.Clear();
            foreach (var element in working)
            {
                list.Add(element);
            }

            return true;
        }

        if (!list.IsReadOnly && list.Count == working.Count)
        {
            for (var i = 0; i < working.Count; i++)
            {
                list[i] = working[i];
            }

            return true;
        }

        return false;
    }

    /// <summary>
    ///     Builds a new collection of a property type holding the given elements.
    /// </summary>
    /// <param name="collectionType">The property type.</param>
    /// <param name="elementType">The element type.</param>
    /// <param name="working">The elements.</param>
    /// <returns>The new collection.</returns>
    private static object CreateCollection(Type collectionType, Type elementType, IReadOnlyList<object?> working)
    {
        if (collectionType.IsArray)
        {
            var array = Array.CreateInstance(elementType, working.Count);
            for (var i = 0; i < working.Count; i++)
            {
                array.SetValue(working[i], i);
            }

            return array;
        }

        var listType = typeof(List<>).MakeGenericType(elementType);
        if (collectionType.IsAssignableFrom(listType))
        {
            var created = (IList)Activator.CreateInstance(listType)!;
            foreach (var element in working)
            {
                created.Add(element);
            }

            return created;
        }

        if (!collectionType.IsAbstract && !collectionType.IsInterface && collectionType.GetConstructor(Type.EmptyTypes) is not null
            && FillInstance(Activator.CreateInstance(collectionType)!, elementType, working) is { } filled)
        {
            return filled;
        }

        throw new NotSupportedException($"A new collection of type {collectionType.Name} cannot be created; supply a settable property or an editable instance.");
    }

    /// <summary>
    ///     Adds the elements to a new collection instance through <see cref="IList"/> or <c>ICollection&lt;T&gt;</c>.
    /// </summary>
    /// <param name="instance">The new, empty collection.</param>
    /// <param name="elementType">The element type.</param>
    /// <param name="working">The elements.</param>
    /// <returns>The instance when it could be filled; otherwise <see langword="null"/>.</returns>
    private static object? FillInstance(object instance, Type elementType, IReadOnlyList<object?> working)
    {
        if (instance is IList list)
        {
            foreach (var element in working)
            {
                list.Add(element);
            }

            return instance;
        }

        if (CollectionTypes.GetGenericCollectionInterface(instance, elementType) is { } generic)
        {
            var add = generic.GetMethod(nameof(ICollection<>.Add))!;
            foreach (var element in working)
            {
                add.Invoke(instance, [element]);
            }

            return instance;
        }

        return null;
    }

    /// <summary>
    ///     Reads <c>ICollection&lt;T&gt;.IsReadOnly</c> of an instance.
    /// </summary>
    /// <param name="generic">The closed <c>ICollection&lt;T&gt;</c> type.</param>
    /// <param name="instance">The instance.</param>
    /// <returns>The value of the property.</returns>
    private static bool IsReadOnlyGeneric(Type generic, object instance) =>
        (bool)generic.GetProperty(nameof(ICollection<>.IsReadOnly))!.GetValue(instance)!;

    /// <summary>
    ///     Creates a new element for a collection with its default constructor.
    /// </summary>
    /// <param name="elementType">The element type.</param>
    /// <param name="value">The new element.</param>
    /// <param name="error">The reason the element cannot be created.</param>
    /// <returns><see langword="true"/> when an element was created.</returns>
    internal static bool TryCreateElement(Type elementType, out object? value, out string? error)
    {
        error = null;
        value = null;
        if (elementType == typeof(string))
        {
            value = string.Empty;
            return true;
        }

        if (Nullable.GetUnderlyingType(elementType) is not null)
        {
            return true;
        }

        if (elementType.IsValueType)
        {
            value = Activator.CreateInstance(elementType);
            return true;
        }

        if (!elementType.IsAbstract && !elementType.IsInterface && elementType.GetConstructor(BindingFlags.Public | BindingFlags.Instance, Type.EmptyTypes) is not null)
        {
            try
            {
                value = Activator.CreateInstance(elementType);
                return true;
            }
            catch (Exception ex) when (!PropertyErrors.IsFatal(ex))
            {
                error = PropertyErrors.Message(ex);
                return false;
            }
        }

        error = $"A new {elementType.Name} cannot be created without a public default constructor; set CollectionItemFactory.";
        return false;
    }
}
