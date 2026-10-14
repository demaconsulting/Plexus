namespace DemaConsulting.Plexus.Avalonia.PropertyGrid;

/// <summary>
///     A source over a list of items supplied by the host, with no reflection. It is the building block for adapters
///     that expose their own data (for example a node name, a property bag and host data) as grid rows.
/// </summary>
/// <remarks>
///     The item list is fixed until the host calls <see cref="Add"/>, <see cref="Remove"/> or <see cref="Refresh"/>,
///     each of which raises <see cref="Changed"/>. When the host changes a value behind the grid's back it should call
///     <see cref="NotifyValueChanged"/> so the grid re-reads it.
/// </remarks>
/// <example>
///     <code><![CDATA[
///     using DemaConsulting.Plexus;
///     using DemaConsulting.Plexus.Avalonia.PropertyGrid;
///
///     // node: a Node of a diagram
///     var source = new CustomPropertySource();
///     source.Add(new CustomPropertyItem("Name", typeof(string), () => node.Name, v => node.Name = (string)v!));
///     source.Add(new CustomPropertyItem("Id", typeof(string), () => node.Id));   // read-only: no setter
///     source.Changed += (_, e) => Console.WriteLine($"changed: {e.PropertyName}");
///     ]]></code>
/// </example>
public sealed class CustomPropertySource : IPropertySource
{
    /// <summary>
    ///     The items.
    /// </summary>
    private readonly List<PropertyItem> _items = [];

    /// <summary>
    ///     Initializes a new instance of the <see cref="CustomPropertySource"/> class.
    /// </summary>
    /// <param name="items">The initial items; <see langword="null"/> means none.</param>
    public CustomPropertySource(IEnumerable<PropertyItem>? items = null)
    {
        if (items is not null)
        {
            _items.AddRange(items);
        }
    }

    /// <inheritdoc />
    public event EventHandler<PropertySourceChangedEventArgs>? Changed;

    /// <inheritdoc />
    public IReadOnlyList<PropertyItem> GetItems() => _items.Where(i => i.IsBrowsable).ToList();

    /// <summary>
    ///     Adds an item and raises <see cref="Changed"/> for everything.
    /// </summary>
    /// <param name="item">The item.</param>
    /// <exception cref="ArgumentNullException"><paramref name="item"/> is <see langword="null"/>.</exception>
    public void Add(PropertyItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        _items.Add(item);
        Refresh();
    }

    /// <summary>
    ///     Removes an item and raises <see cref="Changed"/> for everything when it was present.
    /// </summary>
    /// <param name="item">The item.</param>
    /// <returns><see langword="true"/> when the item was present.</returns>
    public bool Remove(PropertyItem item)
    {
        var removed = _items.Remove(item);
        if (removed)
        {
            Refresh();
        }

        return removed;
    }

    /// <summary>
    ///     Raises <see cref="Changed"/> for everything (call after changing the list of items indirectly).
    /// </summary>
    public void Refresh() => Changed?.Invoke(this, new PropertySourceChangedEventArgs());

    /// <summary>
    ///     Raises <see cref="Changed"/> for one property whose value changed behind the grid's back.
    /// </summary>
    /// <param name="propertyName">The property name.</param>
    public void NotifyValueChanged(string propertyName) => Changed?.Invoke(this, new PropertySourceChangedEventArgs(propertyName));
}
