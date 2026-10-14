using Avalonia.Controls;

namespace DemaConsulting.Plexus.Avalonia.PropertyGrid;

/// <content>
///     The content of the grid: the source, the edit session, and the flat list of rows derived from them.
/// </content>
public partial class PropertyGrid
{
    /// <summary>
    ///     The deepest nesting the search follows (protects against object graphs that refer back to themselves).
    /// </summary>
    private const int MaxSearchDepth = 6;

    /// <summary>
    ///     The row views by key (kept so that editors and their state survive a refresh).
    /// </summary>
    private Dictionary<string, GridRowView> _views = [];

    /// <summary>
    ///     The key of the selected row.
    /// </summary>
    private string? _selectedKey;

    /// <summary>
    ///     The source currently shown.
    /// </summary>
    private IPropertySource? _shownSource;

    /// <summary>
    ///     Whether the grid created the shown source (and therefore disposes it).
    /// </summary>
    private bool _ownsSource;

    /// <summary>
    ///     Whether <see cref="SelectedObject"/> and <see cref="Source"/> are being synchronized.
    /// </summary>
    private bool _syncingContent;

    /// <summary>
    ///     One row of the flat row list.
    /// </summary>
    /// <param name="Key">The row key.</param>
    /// <param name="Category">The category name of a category row, else <see langword="null"/>.</param>
    /// <param name="Item">The item of a property row, else <see langword="null"/>.</param>
    /// <param name="Depth">The nesting depth.</param>
    /// <param name="Expandable">Whether the row shows an expander.</param>
    /// <param name="Expanded">Whether the row is expanded.</param>
    private sealed record RowSpec(string Key, string? Category, PropertyItem? Item, int Depth, bool Expandable, bool Expanded);

    /// <summary>
    ///     Reacts to a change of <see cref="SelectedObject"/> or <see cref="Source"/>.
    /// </summary>
    /// <param name="fromSource">Whether <see cref="Source"/> changed.</param>
    private void OnContentChanged(bool fromSource)
    {
        if (_syncingContent)
        {
            return;
        }

        _syncingContent = true;
        try
        {
            if (fromSource)
            {
                SetCurrentValue(SelectedObjectProperty, null);
            }
            else
            {
                SetCurrentValue(SourceProperty, null);
            }
        }
        finally
        {
            _syncingContent = false;
        }

        AttachSource();
    }

    /// <summary>
    ///     Replaces the shown source and starts a new session.
    /// </summary>
    private void AttachSource()
    {
        DetachSource();
        var target = SelectedObject;
        var source = Source;
        if (source is null && target is not null)
        {
            source = PropertySourceFactory.Create(target);
            _ownsSource = !ReferenceEquals(source, target);
        }

        _shownSource = source;
        if (source is not null)
        {
            source.Changed += OnSourceChanged;
        }

        _expandedPaths.Clear();
        _selectedKey = null;
        CreateSession();
        UpdateDescription();
    }

    /// <summary>
    ///     Stops showing the current source.
    /// </summary>
    private void DetachSource()
    {
        if (_shownSource is { } source)
        {
            source.Changed -= OnSourceChanged;
            if (_ownsSource && source is IDisposable disposable)
            {
                disposable.Dispose();
            }
        }

        _shownSource = null;
        _ownsSource = false;
    }

    /// <summary>
    ///     Starts a new session for the current source and edit mode.
    /// </summary>
    private void RecreateSession()
    {
        CreateSession();
    }

    /// <summary>
    ///     Creates the session and rebuilds the rows.
    /// </summary>
    private void CreateSession()
    {
        if (_session is { } old)
        {
            old.Changed -= OnSessionChanged;
        }

        PropertyEditSession? session = null;
        if (_shownSource is { } source)
        {
            session = new PropertyEditSession(source, EditMode, apply => (CommitScope ?? (run => run()))(apply))
            {
                CollectionItemFactory = item => CollectionItemFactory?.Invoke(item),
            };
            session.Changed += OnSessionChanged;
        }

        Session = session;
        RebuildFromScratch();
    }

    /// <summary>
    ///     Discards every row view and builds the rows again.
    /// </summary>
    private void RebuildFromScratch()
    {
        _views = [];
        _rowsPanel.Children.Clear();
        RefreshRows();
    }

    /// <summary>
    ///     Reacts to a change in the source (the object raised a change notification).
    /// </summary>
    /// <param name="sender">The source.</param>
    /// <param name="e">The event data.</param>
    private void OnSourceChanged(object? sender, PropertySourceChangedEventArgs e) => RefreshRows();

    /// <summary>
    ///     Reacts to a change in the session (a write, a pending edit, an error).
    /// </summary>
    /// <param name="sender">The session.</param>
    /// <param name="e">The event data.</param>
    private void OnSessionChanged(object? sender, PropertySessionChangedEventArgs e) => RefreshRows();

    /// <summary>
    ///     Recomputes the row list and brings the views up to date, keeping the views (and the editors in them) of rows
    ///     that stay.
    /// </summary>
    private void RefreshRows()
    {
        var specs = BuildSpecs();
        var next = new Dictionary<string, GridRowView>(StringComparer.Ordinal);
        var ordered = new List<GridRowView>(specs.Count);
        foreach (var spec in specs)
        {
            GridRowView view;
            if (_views.TryGetValue(spec.Key, out var existing) && ReferenceEquals(existing.Item, spec.Item))
            {
                view = existing;
            }
            else
            {
                view = spec.Item is null
                    ? new CategoryRowView(this, spec.Category!, spec.Expanded)
                    : new PropertyRowView(this, spec.Item);
                if (view is PropertyRowView created)
                {
                    created.SetNameWidth(Math.Max(30, NameColumnWidth));
                }
            }

            switch (view)
            {
                case PropertyRowView property:
                    property.Update(spec.Depth, spec.Expandable, spec.Expanded);
                    break;
                case CategoryRowView category:
                    category.Update(spec.Expanded);
                    break;
            }

            next[spec.Key] = view;
            ordered.Add(view);
        }

        if (!_rowsPanel.Children.SequenceEqual(ordered))
        {
            _rowsPanel.Children.Clear();
            _rowsPanel.Children.AddRange(ordered);
        }

        _views = next;
        if (_selectedKey is not null && !next.ContainsKey(_selectedKey))
        {
            _selectedKey = null;
        }

        foreach (var (key, view) in next)
        {
            view.SetSelected(key == _selectedKey);
        }

        SetAndRaise(SelectedItemProperty, ref _selection, _selectedKey is not null && next.TryGetValue(_selectedKey, out var selected) ? selected.Item : null);
        UpdateDescription();
        UpdateEmptyMessage(specs.Count);
    }

    /// <summary>
    ///     Shows the message that tells why there are no rows.
    /// </summary>
    /// <param name="rowCount">The number of rows.</param>
    private void UpdateEmptyMessage(int rowCount)
    {
        _emptyText.IsVisible = rowCount == 0;
        if (Session is null)
        {
            _emptyText.Text = "No object selected.";
        }
        else
        {
            _emptyText.Text = string.IsNullOrWhiteSpace(Filter) ? "This object has no properties." : "No matching properties.";
        }
    }

    /// <summary>
    ///     Computes the flat list of rows for the current source, sort mode, filter and expansion state.
    /// </summary>
    /// <returns>The rows in display order.</returns>
    private List<RowSpec> BuildSpecs()
    {
        var specs = new List<RowSpec>();
        if (_shownSource is null)
        {
            return specs;
        }

        var filter = Filter?.Trim();
        var filtering = !string.IsNullOrEmpty(filter);
        foreach (var group in PropertyOrdering.Group(_shownSource.GetItems(), SortMode))
        {
            var categoryMatches = filtering && group.Category is not null && group.Category.Contains(filter!, StringComparison.OrdinalIgnoreCase);
            var visible = filtering && !categoryMatches ? group.Items.Where(i => Matches(i, filter!, 0)).ToList() : [.. group.Items];
            if (visible.Count == 0)
            {
                continue;
            }

            if (group.Category is { } category)
            {
                var expanded = filtering || IsCategoryExpanded(category);
                specs.Add(new RowSpec(CategoryKey(category), category, null, 0, true, expanded));
                if (!expanded)
                {
                    continue;
                }
            }

            foreach (var item in visible)
            {
                AddItem(specs, item, 0, categoryMatches ? null : filter);
            }
        }

        return specs;
    }

    /// <summary>
    ///     Adds an item and, when it is expanded, its nested properties.
    /// </summary>
    /// <param name="specs">The row list.</param>
    /// <param name="item">The item.</param>
    /// <param name="depth">The nesting depth.</param>
    /// <param name="filter">The active filter, or <see langword="null"/>.</param>
    private void AddItem(List<RowSpec> specs, PropertyItem item, int depth, string? filter)
    {
        var expandable = item.IsExpandable && !item.IsCollection;
        var expanded = expandable && (_expandedPaths.Contains(item.Path) || (!string.IsNullOrEmpty(filter) && depth < MaxSearchDepth && item.Children.Any(c => Matches(c, filter, depth + 1))));
        specs.Add(new RowSpec(PropertyKey(item.Path), null, item, depth, expandable, expanded));
        if (!expanded)
        {
            return;
        }

        var matching = !string.IsNullOrEmpty(filter) && !NameMatches(item, filter);
        foreach (var child in item.Children.Where(c => !matching || Matches(c, filter!, depth + 1)))
        {
            AddItem(specs, child, depth + 1, matching ? filter : null);
        }
    }

    /// <summary>
    ///     Checks whether an item or any of its nested properties matches the filter.
    /// </summary>
    /// <param name="item">The item.</param>
    /// <param name="filter">The filter text.</param>
    /// <param name="depth">The nesting depth.</param>
    /// <returns><see langword="true"/> when the item should be shown.</returns>
    private static bool Matches(PropertyItem item, string filter, int depth) =>
        NameMatches(item, filter) || (depth < MaxSearchDepth && item.IsExpandable && !item.IsCollection && item.Children.Any(c => Matches(c, filter, depth + 1)));

    /// <summary>
    ///     Checks whether the name of an item contains the filter text.
    /// </summary>
    /// <param name="item">The item.</param>
    /// <param name="filter">The filter text.</param>
    /// <returns><see langword="true"/> when the display name or name matches.</returns>
    private static bool NameMatches(PropertyItem item, string filter) =>
        item.DisplayName.Contains(filter, StringComparison.OrdinalIgnoreCase) || item.Name.Contains(filter, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    ///     Selects a row.
    /// </summary>
    /// <param name="key">The row key, or <see langword="null"/> to clear the selection.</param>
    /// <param name="focus">Whether the grid takes the keyboard focus.</param>
    internal void SelectKey(string? key, bool focus)
    {
        if (key is not null && !_views.ContainsKey(key))
        {
            key = null;
        }

        if (_selectedKey != key)
        {
            if (_selectedKey is not null && _views.TryGetValue(_selectedKey, out var previous))
            {
                previous.SetSelected(false);
            }

            _selectedKey = key;
            GridRowView? current = null;
            if (key is not null && _views.TryGetValue(key, out current))
            {
                current.SetSelected(true);
            }

            SetAndRaise(SelectedItemProperty, ref _selection, current?.Item);
            UpdateDescription();
        }

        if (focus)
        {
            Focus();
        }
    }
}
