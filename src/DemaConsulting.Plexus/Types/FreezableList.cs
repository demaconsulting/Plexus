using System.Collections;

namespace DemaConsulting.Plexus;

/// <summary>
///     A list that can be locked once its owner has been registered, so definitions cannot change under live nodes.
/// </summary>
/// <typeparam name="T">The element type.</typeparam>
/// <remarks>
///     Node types expose their ports and properties as plain <see cref="IList{T}"/> so collection
///     initializers work, but nodes cache ports derived from them. Freezing at registration time
///     keeps those caches valid. For port lists these are the type's declared defaults: a type with a
///     <see cref="NodeType.PortProvider"/> supplies each node's ports itself. Not thread-safe.
/// </remarks>
internal sealed class FreezableList<T> : IList<T>
    where T : class
{
    /// <summary>
    ///     The backing list.
    /// </summary>
    private readonly List<T> _items = [];

    /// <summary>
    ///     Whether modification is forbidden.
    /// </summary>
    private bool _frozen;

    /// <inheritdoc />
    public int Count => _items.Count;

    /// <inheritdoc />
    public bool IsReadOnly => _frozen;

    /// <inheritdoc />
    public T this[int index]
    {
        get => _items[index];
        set
        {
            EnsureMutable();
            _items[index] = Validate(value);
        }
    }

    /// <summary>
    ///     Forbids all further modification.
    /// </summary>
    public void Freeze() => _frozen = true;

    /// <inheritdoc />
    public void Add(T item)
    {
        EnsureMutable();
        _items.Add(Validate(item));
    }

    /// <inheritdoc />
    public void Clear()
    {
        EnsureMutable();
        _items.Clear();
    }

    /// <inheritdoc />
    public bool Contains(T item) => _items.Contains(item);

    /// <inheritdoc />
    public void CopyTo(T[] array, int arrayIndex) => _items.CopyTo(array, arrayIndex);

    /// <inheritdoc />
    public int IndexOf(T item) => _items.IndexOf(item);

    /// <inheritdoc />
    public void Insert(int index, T item)
    {
        EnsureMutable();
        _items.Insert(index, Validate(item));
    }

    /// <inheritdoc />
    public bool Remove(T item)
    {
        EnsureMutable();
        return _items.Remove(item);
    }

    /// <inheritdoc />
    public void RemoveAt(int index)
    {
        EnsureMutable();
        _items.RemoveAt(index);
    }

    /// <inheritdoc />
    public IEnumerator<T> GetEnumerator() => _items.GetEnumerator();

    /// <inheritdoc />
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    /// <summary>
    ///     Throws when the list is frozen.
    /// </summary>
    /// <exception cref="NotSupportedException">Thrown when the list is frozen.</exception>
    private void EnsureMutable()
    {
        if (_frozen)
        {
            throw new NotSupportedException("The node type has been registered and can no longer be modified.");
        }
    }

    /// <summary>
    ///     Rejects null elements.
    /// </summary>
    /// <param name="item">The element to check.</param>
    /// <returns>The same element.</returns>
    /// <exception cref="ArgumentNullException">Thrown when the element is null.</exception>
    private static T Validate(T item)
    {
        ArgumentNullException.ThrowIfNull(item);
        return item;
    }
}
