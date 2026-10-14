namespace DemaConsulting.Plexus.Avalonia.PropertyGrid;

/// <summary>
///     Merges several <see cref="IPropertySource"/> instances into one, in order. Use it to show data from different
///     origins in one grid (for example a node's name, its property bag and host data).
/// </summary>
/// <remarks>
///     Items keep their source's category, so merged sources can share categories. If two sources supply an item with
///     the same <see cref="PropertyItem.Name"/>, the item of the earlier source is kept and the later one is omitted,
///     because the name is the item's path and must be unique. <see cref="Changed"/> forwards the change events of all
///     sources. Dispose the composite to detach from its sources (the sources themselves are not disposed). The composite
///     is also an <see cref="IPropertyCommitScope"/> that chains the commit scopes of the sources that have one.
/// </remarks>
/// <example>
///     <code><![CDATA[
///     var nameSource = new CustomPropertySource([new CustomPropertyItem("Name", typeof(string), () => node.Name)]);
///     var targetSource = new ReflectionPropertySource(node.Settings);
///
///     using var merged = new CompositePropertySource(nameSource, targetSource);
///     IReadOnlyList<PropertyItem> items = merged.GetItems();   // Name first, then the settings
///     ]]></code>
/// </example>
public sealed class CompositePropertySource : IPropertySource, IPropertyCommitScope, IDisposable
{
    /// <summary>
    ///     The merged sources.
    /// </summary>
    private readonly List<IPropertySource> _sources = [];

    /// <summary>
    ///     Initializes a new instance of the <see cref="CompositePropertySource"/> class.
    /// </summary>
    /// <param name="sources">The sources to merge, in order.</param>
    /// <exception cref="ArgumentNullException"><paramref name="sources"/> is <see langword="null"/> or contains <see langword="null"/>.</exception>
    public CompositePropertySource(params IPropertySource[] sources)
        : this((IEnumerable<IPropertySource>)sources)
    {
    }

    /// <summary>
    ///     Initializes a new instance of the <see cref="CompositePropertySource"/> class.
    /// </summary>
    /// <param name="sources">The sources to merge, in order.</param>
    /// <exception cref="ArgumentNullException"><paramref name="sources"/> is <see langword="null"/> or contains <see langword="null"/>.</exception>
    public CompositePropertySource(IEnumerable<IPropertySource> sources)
    {
        ArgumentNullException.ThrowIfNull(sources);
        foreach (var source in sources)
        {
            ArgumentNullException.ThrowIfNull(source);
            Attach(source);
        }
    }

    /// <inheritdoc />
    public event EventHandler<PropertySourceChangedEventArgs>? Changed;

    /// <summary>
    ///     Gets the merged sources in order.
    /// </summary>
    public IReadOnlyList<IPropertySource> Sources => _sources;

    /// <inheritdoc />
    public IReadOnlyList<PropertyItem> GetItems()
    {
        var names = new HashSet<string>(StringComparer.Ordinal);
        var items = new List<PropertyItem>();
        foreach (var source in _sources)
        {
            items.AddRange(source.GetItems().Where(item => names.Add(item.Name)));
        }

        return items;
    }

    /// <summary>
    ///     Appends a source and raises <see cref="Changed"/> for everything.
    /// </summary>
    /// <param name="source">The source.</param>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> is <see langword="null"/>.</exception>
    public void Add(IPropertySource source)
    {
        ArgumentNullException.ThrowIfNull(source);
        Attach(source);
        Changed?.Invoke(this, new PropertySourceChangedEventArgs());
    }

    /// <summary>
    ///     Removes a source and raises <see cref="Changed"/> for everything when it was present.
    /// </summary>
    /// <param name="source">The source.</param>
    /// <returns><see langword="true"/> when the source was present.</returns>
    public bool Remove(IPropertySource source)
    {
        if (!_sources.Remove(source))
        {
            return false;
        }

        source.Changed -= OnSourceChanged;
        Changed?.Invoke(this, new PropertySourceChangedEventArgs());
        return true;
    }

    /// <summary>
    ///     Runs the writes of a commit inside the commit scopes of the merged sources that have one (the first source's
    ///     scope is outermost); a composite with no such source simply runs the writes.
    /// </summary>
    /// <param name="apply">The action that performs the writes; it is invoked exactly once.</param>
    public void RunCommit(Action apply)
    {
        ArgumentNullException.ThrowIfNull(apply);
        var scopes = _sources.OfType<IPropertyCommitScope>().ToList();
        var run = apply;
        for (var i = scopes.Count - 1; i >= 0; i--)
        {
            var scope = scopes[i];
            var inner = run;
            run = () => scope.RunCommit(inner);
        }

        run();
    }

    /// <summary>
    ///     Detaches from all sources.
    /// </summary>
    public void Dispose()
    {
        foreach (var source in _sources)
        {
            source.Changed -= OnSourceChanged;
        }
    }

    /// <summary>
    ///     Adds a source and subscribes to its changes.
    /// </summary>
    /// <param name="source">The source.</param>
    private void Attach(IPropertySource source)
    {
        _sources.Add(source);
        source.Changed += OnSourceChanged;
    }

    /// <summary>
    ///     Forwards a change of a source.
    /// </summary>
    /// <param name="sender">The source.</param>
    /// <param name="e">The change.</param>
    private void OnSourceChanged(object? sender, PropertySourceChangedEventArgs e) => Changed?.Invoke(this, e);
}
