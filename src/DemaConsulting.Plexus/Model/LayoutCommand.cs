namespace DemaConsulting.Plexus;

/// <summary>
///     A mergeable command that changes element bounds, group positions and wire routes together, as one move or resize.
/// </summary>
/// <remarks>
///     Moves must change nodes and the routes of attached wires atomically, and a drag produces
///     many such commands. Storing absolute old/new states per target makes the command exact in both
///     directions and lets consecutive layout commands on the same page merge into one compact
///     entry (first old state, last new state).
/// </remarks>
internal sealed class LayoutCommand : IMergeableCommand
{
    /// <summary>
    ///     The page the layout change belongs to; only commands for the same page merge.
    /// </summary>
    private readonly DiagramPage _page;

    /// <summary>
    ///     Bounds changes of nodes and annotations.
    /// </summary>
    private readonly ChangeSet<DiagramElement, Rect> _bounds = new();

    /// <summary>
    ///     Collapsed-position changes of groups.
    /// </summary>
    private readonly ChangeSet<Group, Point> _positions = new();

    /// <summary>
    ///     Route changes of wires.
    /// </summary>
    private readonly ChangeSet<Wire, Point[]> _routes = new();

    /// <summary>
    ///     Initializes a new instance of the <see cref="LayoutCommand"/> class.
    /// </summary>
    /// <param name="description">Human-readable description.</param>
    /// <param name="page">The page whose elements change.</param>
    public LayoutCommand(string description, DiagramPage page)
    {
        Description = description;
        _page = page;
    }

    /// <inheritdoc />
    public string Description { get; }

    /// <summary>
    ///     Gets a value indicating whether the command changes nothing.
    /// </summary>
    public bool IsEmpty => _bounds.Count == 0 && _positions.Count == 0 && _routes.Count == 0;

    /// <summary>
    ///     Adds a bounds change of a node or annotation.
    /// </summary>
    /// <param name="element">The node or annotation.</param>
    /// <param name="oldBounds">The current bounds.</param>
    /// <param name="newBounds">The new bounds.</param>
    public void AddBounds(DiagramElement element, Rect oldBounds, Rect newBounds) => _bounds.Add(element, oldBounds, newBounds);

    /// <summary>
    ///     Adds a collapsed-position change of a group.
    /// </summary>
    /// <param name="group">The group.</param>
    /// <param name="oldPosition">The current collapsed position.</param>
    /// <param name="newPosition">The new collapsed position.</param>
    public void AddPosition(Group group, Point oldPosition, Point newPosition) => _positions.Add(group, oldPosition, newPosition);

    /// <summary>
    ///     Adds a route change of a wire.
    /// </summary>
    /// <param name="wire">The wire.</param>
    /// <param name="oldRoute">The current route.</param>
    /// <param name="newRoute">The new route.</param>
    public void AddRoute(Wire wire, Point[] oldRoute, Point[] newRoute) => _routes.Add(wire, oldRoute, newRoute);

    /// <inheritdoc />
    public void Do() => Apply(useNew: true);

    /// <inheritdoc />
    public void Undo() => Apply(useNew: false);

    /// <inheritdoc />
    public bool TryMerge(IUndoableCommand next)
    {
        if (next is not LayoutCommand other || !ReferenceEquals(other._page, _page))
        {
            return false;
        }

        // Keep this command's old states and take the other's new states
        _bounds.Merge(other._bounds);
        _positions.Merge(other._positions);
        _routes.Merge(other._routes);
        return true;
    }

    /// <summary>
    ///     Applies either the new or the old state of every change and reports affected group bounds once.
    /// </summary>
    /// <param name="useNew">True to apply new states (do/redo); false to apply old states (undo).</param>
    private void Apply(bool useNew)
    {
        // Bounds first so routes (which depend on anchors) are consistent when notifications fire
        var affectedGroups = new HashSet<Group>();
        foreach (var (element, value) in _bounds.Values(useNew))
        {
            if (element is Node node)
            {
                node.SetBoundsCore(value);
            }
            else if (element is Annotation annotation)
            {
                annotation.SetBoundsCore(value);
            }

            CollectGroups(element.Parent, affectedGroups);
        }

        foreach (var (group, value) in _positions.Values(useNew))
        {
            group.SetCollapsedPositionCore(value);
            affectedGroups.Add(group);
            CollectGroups(group.Parent, affectedGroups);
        }

        foreach (var (wire, value) in _routes.Values(useNew))
        {
            wire.SetRouteCore(value);
        }

        foreach (var group in affectedGroups)
        {
            group.NotifyChanged(nameof(Group.Bounds));
        }
    }

    /// <summary>
    ///     Adds a group and all of its ancestors to a set.
    /// </summary>
    /// <param name="start">The first group, or null.</param>
    /// <param name="groups">The set to fill.</param>
    private static void CollectGroups(Group? start, HashSet<Group> groups)
    {
        for (var group = start; group is not null; group = group.Parent)
        {
            groups.Add(group);
        }
    }

    /// <summary>
    ///     An insertion-ordered set of old/new value pairs keyed by target, used to apply and merge changes deterministically.
    /// </summary>
    /// <typeparam name="TKey">The target type.</typeparam>
    /// <typeparam name="TValue">The value type.</typeparam>
    private sealed class ChangeSet<TKey, TValue>
        where TKey : class
    {
        /// <summary>
        ///     The changes in the order they were first added.
        /// </summary>
        private readonly List<Change> _changes = [];

        /// <summary>
        ///     Position of each target in <see cref="_changes"/>.
        /// </summary>
        private readonly Dictionary<TKey, int> _index = [];

        /// <summary>
        ///     Gets the number of targets.
        /// </summary>
        public int Count => _changes.Count;

        /// <summary>
        ///     Adds a change, or extends an existing change of the same target to the new value.
        /// </summary>
        /// <param name="key">The target.</param>
        /// <param name="oldValue">The value before the change.</param>
        /// <param name="newValue">The value after the change.</param>
        public void Add(TKey key, TValue oldValue, TValue newValue)
        {
            if (_index.TryGetValue(key, out var position))
            {
                _changes[position] = _changes[position] with { New = newValue };
                return;
            }

            _index[key] = _changes.Count;
            _changes.Add(new Change(key, oldValue, newValue));
        }

        /// <summary>
        ///     Merges a later set into this one.
        /// </summary>
        /// <param name="other">The later changes.</param>
        public void Merge(ChangeSet<TKey, TValue> other)
        {
            foreach (var change in other._changes)
            {
                Add(change.Key, change.Old, change.New);
            }
        }

        /// <summary>
        ///     Enumerates the targets with either their old or new values.
        /// </summary>
        /// <param name="useNew">True for new values; false for old values.</param>
        /// <returns>The target/value pairs in insertion order.</returns>
        public IEnumerable<(TKey Key, TValue Value)> Values(bool useNew) =>
            _changes.Select(c => (c.Key, useNew ? c.New : c.Old));

        /// <summary>
        ///     One target's old and new values.
        /// </summary>
        /// <param name="Key">The target.</param>
        /// <param name="Old">The value before the change.</param>
        /// <param name="New">The value after the change.</param>
        private readonly record struct Change(TKey Key, TValue Old, TValue New);
    }
}
