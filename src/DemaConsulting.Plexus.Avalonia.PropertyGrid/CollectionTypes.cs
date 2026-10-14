using System.Collections;

namespace DemaConsulting.Plexus.Avalonia.PropertyGrid;

/// <summary>
///     Recognizes collection property types and finds their element type.
/// </summary>
internal static class CollectionTypes
{
    /// <summary>
    ///     Tries to treat a type as an editable or viewable collection.
    /// </summary>
    /// <param name="type">The property type.</param>
    /// <param name="elementType">The element type when the type is a collection.</param>
    /// <returns>
    ///     <see langword="true"/> for arrays, <see cref="IList"/>, <c>ICollection&lt;T&gt;</c> and
    ///     <c>IReadOnlyCollection&lt;T&gt;</c> types; <see langword="false"/> for strings and dictionaries.
    /// </returns>
    internal static bool TryGetElementType(Type type, out Type elementType)
    {
        elementType = typeof(object);
        if (type == typeof(string) || type == typeof(object))
        {
            return false;
        }

        if (type.IsArray)
        {
            elementType = type.GetElementType()!;
            return type.GetArrayRank() == 1;
        }

        var interfaces = type.IsInterface ? [type, .. type.GetInterfaces()] : type.GetInterfaces();
        if (interfaces.Any(IsDictionary))
        {
            return false;
        }

        foreach (var candidate in interfaces)
        {
            if (candidate.IsGenericType && candidate.GetGenericTypeDefinition() == typeof(ICollection<>))
            {
                elementType = candidate.GetGenericArguments()[0];
                return true;
            }
        }

        foreach (var candidate in interfaces)
        {
            if (candidate.IsGenericType && candidate.GetGenericTypeDefinition() == typeof(IReadOnlyCollection<>))
            {
                elementType = candidate.GetGenericArguments()[0];
                return true;
            }
        }

        return interfaces.Contains(typeof(IList));
    }

    /// <summary>
    ///     Reports whether an interface is a dictionary interface.
    /// </summary>
    /// <param name="candidate">The interface.</param>
    /// <returns><see langword="true"/> for the dictionary interfaces.</returns>
    private static bool IsDictionary(Type candidate) =>
        candidate == typeof(IDictionary) ||
        (candidate.IsGenericType &&
         (candidate.GetGenericTypeDefinition() == typeof(IDictionary<,>) ||
          candidate.GetGenericTypeDefinition() == typeof(IReadOnlyDictionary<,>)));

    /// <summary>
    ///     Finds the generic <c>ICollection&lt;T&gt;</c> interface of a collection instance.
    /// </summary>
    /// <param name="collection">The collection.</param>
    /// <param name="elementType">The element type.</param>
    /// <returns>The closed interface type, or <see langword="null"/> when the instance does not implement it.</returns>
    internal static Type? GetGenericCollectionInterface(object collection, Type elementType)
    {
        var closed = typeof(ICollection<>).MakeGenericType(elementType);
        return closed.IsInstanceOfType(collection) ? closed : null;
    }

    /// <summary>
    ///     Takes a snapshot of the elements of a collection instance.
    /// </summary>
    /// <param name="collection">The collection, or <see langword="null"/>.</param>
    /// <returns>The elements in enumeration order; empty for <see langword="null"/>.</returns>
    internal static List<object?> Snapshot(object? collection)
    {
        var result = new List<object?>();
        if (collection is IEnumerable enumerable)
        {
            foreach (var element in enumerable)
            {
                result.Add(element);
            }
        }

        return result;
    }
}
