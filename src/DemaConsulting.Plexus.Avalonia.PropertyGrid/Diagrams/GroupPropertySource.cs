namespace DemaConsulting.Plexus.Avalonia.PropertyGrid.Diagrams;

/// <summary>
///     Shows and edits one Plexus <see cref="Group"/> in a property grid: its title and whether it is collapsed.
///     <c>grid.SelectedObject = group</c> uses it automatically.
/// </summary>
/// <remarks>
///     Like <see cref="NodePropertySource"/>, the source is an <see cref="IPropertyCommitScope"/>: each commit is one undo
///     entry whatever the grid's <see cref="PropertyEditMode"/>, and the source raises <see cref="Changed"/> when the group
///     changes from elsewhere. Dispose the source to detach from the group.
/// </remarks>
/// <example>
///     <code><![CDATA[
///     using DemaConsulting.Plexus;
///     using DemaConsulting.Plexus.Avalonia.PropertyGrid;
///
///     // group: a Group of a diagram page
///     var grid = new PropertyGrid { EditMode = PropertyEditMode.Live, SelectedObject = group };
///     ]]></code>
/// </example>
public sealed class GroupPropertySource : IPropertySource, IPropertyCommitScope, IDisposable
{
    /// <summary>
    ///     The edited group.
    /// </summary>
    private readonly Group _group;

    /// <summary>
    ///     The commit plumbing.
    /// </summary>
    private readonly ElementEditing _editing;

    /// <summary>
    ///     The items.
    /// </summary>
    private readonly List<PropertyItem> _items;

    /// <summary>
    ///     Initializes a new instance of the <see cref="GroupPropertySource"/> class.
    /// </summary>
    /// <param name="group">The group to show and edit; it must be attached to a diagram.</param>
    /// <exception cref="ArgumentNullException"><paramref name="group"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="group"/> is not attached to a diagram.</exception>
    public GroupPropertySource(Group group)
    {
        ArgumentNullException.ThrowIfNull(group);
        _group = group;
        _editing = new ElementEditing(group, [nameof(Group.Title), nameof(Group.IsCollapsed)], Publish, () => { });
        var work = _editing.Work;
        _items =
        [
            new ElementPropertyItem(
                nameof(Group.Title),
                typeof(string),
                work,
                () => _group.Title,
                new CustomPropertyOptions { Category = "General", Description = "The title shown in the group's title bar." },
                value => value is null ? "The title cannot be empty." : null),
            new ElementPropertyItem(
                nameof(Group.IsCollapsed),
                typeof(bool),
                work,
                () => _group.IsCollapsed,
                new CustomPropertyOptions
                {
                    DisplayName = "Collapsed",
                    Category = "General",
                    Description = "Whether the group hides its members and shows as a single block.",
                    HasDefault = true,
                    DefaultValue = false,
                }),
        ];
        _editing.Changed += (_, e) => Changed?.Invoke(this, e);
    }

    /// <inheritdoc />
    public event EventHandler<PropertySourceChangedEventArgs>? Changed;

    /// <summary>
    ///     Gets the group that is shown and edited.
    /// </summary>
    public Group Group => _group;

    /// <inheritdoc />
    public IReadOnlyList<PropertyItem> GetItems() => _items;

    /// <inheritdoc />
    public void RunCommit(Action apply) => _editing.RunCommit(apply);

    /// <summary>
    ///     Stops listening to the group.
    /// </summary>
    public void Dispose() => _editing.Dispose();

    /// <summary>
    ///     Copies the written values to the group (inside the commit's undo transaction).
    /// </summary>
    private void Publish()
    {
        if (_editing.Work.TryGet(nameof(Group.Title), out var title) && title is string text)
        {
            _group.Title = text;
        }

        if (_editing.Work.TryGet(nameof(Group.IsCollapsed), out var collapsed) && collapsed is bool value)
        {
            _group.IsCollapsed = value;
        }
    }
}
