using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.VisualTree;
using DemaConsulting.Plexus.Scene;

namespace DemaConsulting.Plexus.Avalonia;

/// <content>
///     Building the entries (filter, grouping, headers, prototype items), selection, activation, following an editor and the keyboard.
/// </content>
public sealed partial class NodePalette
{
    /// <summary>
    ///     Whether <see cref="_registry"/> reflects the current <see cref="Types"/>.
    /// </summary>
    private bool _registryResolved;

    /// <summary>
    ///     Whether the palette is in the visual tree, which is when it listens to its editor.
    /// </summary>
    private bool _isAttached;

    /// <summary>
    ///     The horizontal reach, in diagram units, from a node box's center to the edge of the widest visual extent among the shown items; sizes the common box column.
    /// </summary>
    private double _columnReach;

    /// <summary>
    ///     The vertical reach, in diagram units, from a node box's top edge up to the top of the highest visual extent among the shown items; puts every box top on one line.
    /// </summary>
    private double _topReach;

    /// <summary>
    ///     The editor whose property changes the palette currently listens to.
    /// </summary>
    private DiagramEditor? _followedEditor;

    /// <summary>
    ///     The diagram whose <see cref="Diagram.Changed"/> event the palette currently listens to, to follow its theme.
    /// </summary>
    private Diagram? _followedDiagram;

    /// <summary>
    ///     Marks the entries as stale so they are rebuilt at the next layout or query.
    /// </summary>
    private void MarkItemsDirty()
    {
        // A new sequence or a refresh may have changed which types exist, so the registry is resolved again too
        _registryResolved = false;
        _itemsDirty = true;
        InvalidateMeasure();
    }

    /// <summary>
    ///     Rebuilds the headers and items when they are stale.
    /// </summary>
    private void EnsureItems()
    {
        if (!_itemsDirty)
        {
            return;
        }

        _itemsDirty = false;

        // Discard the old entries; item scenes live in the prototype cache, so rebuilding is cheap
        foreach (var old in _items)
        {
            old.Drag.Dispose();
        }

        _items.Clear();
        _stack.Children.Clear();
        if (!_registryResolved)
        {
            _registry = ResolveRegistry();
            _registryResolved = true;
        }

        if (_registry is not { } registry)
        {
            return;
        }

        var visible = registry.Where(Matches).ToList();
        var theme = EffectiveTheme;
        var zoom = Zoom;

        // One box column for the whole palette: the widest visual extent of any shown type, measured from the box center
        _columnReach = visible.Select(type => _prototypes.Get(registry, type, theme, theme, _measurer).HorizontalReach).DefaultIfEmpty(0).Max();
        _topReach = visible.Select(type => -_prototypes.Get(registry, type, theme, theme, _measurer).VisualBounds.Top).DefaultIfEmpty(0).Max();
        if (GroupByCategory && visible.Exists(type => CategoryOf(type) is not null))
        {
            // Categories in registration order of their first type; the uncategorized types close the list
            foreach (var category in visible.Select(CategoryOf).Where(category => category is not null).Distinct(StringComparer.Ordinal))
            {
                AddGroup(registry, category!, visible.Where(type => string.Equals(CategoryOf(type), category, StringComparison.Ordinal)), theme, zoom);
            }

            var uncategorized = visible.Where(type => CategoryOf(type) is null).ToList();
            if (uncategorized.Count > 0)
            {
                AddGroup(registry, UncategorizedHeader, uncategorized, theme, zoom);
            }
        }
        else
        {
            AddItems(registry, visible, theme, zoom);
        }

        UpdateSelectionVisuals();
    }

    /// <summary>
    ///     Adds a header followed by the items of its types.
    /// </summary>
    /// <param name="registry">The registry that holds the types.</param>
    /// <param name="header">The header text.</param>
    /// <param name="types">The types of the group, in order.</param>
    /// <param name="theme">The theme to draw with.</param>
    /// <param name="zoom">The zoom to draw at.</param>
    private void AddGroup(NodeTypeRegistry registry, string header, IEnumerable<NodeType> types, DiagramTheme theme, double zoom)
    {
        // The header borrows the look of a group title on the canvas
        _stack.Children.Add(new Border
        {
            Background = _painter.GetBrush(theme.GroupTitleFill),
            CornerRadius = new CornerRadius(2),
            Padding = new Thickness(6, 2),
            Child = new TextBlock
            {
                Text = header,
                FontFamily = new FontFamily(theme.FontFamily),
                FontSize = theme.GroupTitleFontSize + 1,
                FontWeight = FontWeight.Bold,
                Foreground = _painter.GetBrush(theme.GroupTitleText),
            },
        });
        AddItems(registry, types, theme, zoom);
    }

    /// <summary>
    ///     Adds the items of some types.
    /// </summary>
    /// <param name="registry">The registry that holds the types.</param>
    /// <param name="types">The types, in order.</param>
    /// <param name="theme">The theme to draw with.</param>
    /// <param name="zoom">The zoom to draw at.</param>
    private void AddItems(NodeTypeRegistry registry, IEnumerable<NodeType> types, DiagramTheme theme, double zoom)
    {
        foreach (var type in types)
        {
            // The palette uses one theme for both sizing and drawing, like an editor without a theme override
            var prototype = _prototypes.Get(registry, type, theme, theme, _measurer);
            var item = new NodePaletteItem(this, prototype, _painter, theme, zoom, _columnReach, _topReach);
            _items.Add(item);
            _stack.Children.Add(item);
        }
    }

    /// <summary>
    ///     Gets the category of a type, or null when it has none (null, empty or whitespace).
    /// </summary>
    /// <param name="type">The node type.</param>
    /// <returns>The category, or null.</returns>
    private static string? CategoryOf(NodeType type) => string.IsNullOrWhiteSpace(type.Category) ? null : type.Category;

    /// <summary>
    ///     Determines whether a type passes <see cref="Filter"/>.
    /// </summary>
    /// <param name="type">The node type.</param>
    /// <returns><see langword="true"/> when there is no filter or the display name, title, key or category contains it.</returns>
    private bool Matches(NodeType type)
    {
        var filter = Filter?.Trim();
        if (string.IsNullOrEmpty(filter))
        {
            return true;
        }

        return Contains(type.DisplayName, filter) || Contains(type.Title, filter) || Contains(type.Key, filter) || Contains(type.Category, filter);
    }

    /// <summary>
    ///     Determines whether a text contains another, ignoring case.
    /// </summary>
    /// <param name="text">The text to search, possibly null.</param>
    /// <param name="part">The text to look for.</param>
    /// <returns><see langword="true"/> when found.</returns>
    private static bool Contains(string? text, string part) => text?.Contains(part, StringComparison.OrdinalIgnoreCase) == true;

    /// <summary>
    ///     Highlights the item of <see cref="SelectedNodeType"/> and clears the others.
    /// </summary>
    private void UpdateSelectionVisuals()
    {
        var selected = SelectedNodeType;
        foreach (var item in _items)
        {
            item.IsSelected = selected is not null && ReferenceEquals(item.NodeType, selected);
        }
    }

    /// <summary>
    ///     Starts the node-type drag of an item through <see cref="DragStarter"/>.
    /// </summary>
    /// <param name="press">The press that starts the drag.</param>
    /// <param name="key">The node type key.</param>
    /// <returns>A task that completes when the drag has ended.</returns>
    internal Task StartDragAsync(PointerPressedEventArgs press, string key) => DragStarter(press, key);

    /// <summary>
    ///     Selects the pressed item, takes focus and activates on a double-click.
    /// </summary>
    /// <param name="item">The pressed item.</param>
    /// <param name="clickCount">The click count of the press.</param>
    internal void OnItemPressed(NodePaletteItem item, int clickCount)
    {
        SelectedNodeType = item.NodeType;
        Focus();
        if (clickCount >= 2)
        {
            Activate(item.NodeType);
        }
    }

    /// <summary>
    ///     Activates a node type as if the user had double-clicked it: raises <see cref="NodeTypeActivated"/> and, unless a handler marks it handled, adds the node to the <see cref="Editor"/>.
    /// </summary>
    /// <remarks>
    ///     With an <see cref="Editor"/> set and no handler that sets <see cref="NodeTypeActivatedEventArgs.Handled"/>,
    ///     the node is added at the editor's visible center, selected, as one undo entry
    ///     (<see cref="DiagramEditor.AddNodeAtCenter"/>), and the editor takes the keyboard focus. The type does
    ///     not have to be shown: a filter does not stop a host or a test from activating a type.
    /// </remarks>
    /// <param name="type">The node type to activate.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="type"/> is null.</exception>
    public void Activate(NodeType type)
    {
        ArgumentNullException.ThrowIfNull(type);
        var args = new NodeTypeActivatedEventArgs(type);
        NodeTypeActivated?.Invoke(this, args);

        // The default action belongs to the palette only when it works with an editor and the host did not take over
        if (!args.Handled && Editor is { } editor && editor.AddNodeAtCenter(type) is not null)
        {
            editor.Focus();
        }
    }

    /// <summary>
    ///     Scrolls the palette so the item of a node type is visible.
    /// </summary>
    /// <remarks>
    ///     UI automation clients reach the same function through the ScrollItem pattern of the item, which
    ///     is how a driver reveals an entry that is scrolled out of view before grabbing it.
    /// </remarks>
    /// <param name="type">The node type; a type with the same key counts, so a type of another registry instance works too.</param>
    /// <returns><see langword="true"/> when the type is shown (after <see cref="Filter"/>) and was scrolled to; otherwise <see langword="false"/>.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="type"/> is null.</exception>
    public bool ScrollIntoView(NodeType type)
    {
        ArgumentNullException.ThrowIfNull(type);
        if (FindItem(type) is not { } item)
        {
            return false;
        }

        // Items scroll by their arranged position, so make sure the layout is current first
        UpdateLayout();
        item.BringIntoView();
        return true;
    }

    /// <summary>
    ///     Selects a node type (see <see cref="SelectedNodeType"/>) and scrolls its item into view.
    /// </summary>
    /// <param name="type">The node type; a type with the same key counts.</param>
    /// <returns><see langword="true"/> when the type is shown and was selected; otherwise <see langword="false"/> and the selection is unchanged.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="type"/> is null.</exception>
    public bool Select(NodeType type)
    {
        ArgumentNullException.ThrowIfNull(type);
        if (FindItem(type) is not { } item)
        {
            return false;
        }

        SelectedNodeType = item.NodeType;
        return ScrollIntoView(item.NodeType);
    }

    /// <summary>
    ///     Finds the shown item of a node type.
    /// </summary>
    /// <param name="type">The node type.</param>
    /// <returns>The item of that very type, else the item of the type with the same key, else null.</returns>
    private NodePaletteItem? FindItem(NodeType type)
    {
        EnsureItems();
        return _items.Find(item => ReferenceEquals(item.NodeType, type))
            ?? _items.Find(item => string.Equals(item.NodeType.Key, type.Key, StringComparison.Ordinal));
    }

    /// <summary>
    ///     Makes the editor subscriptions match the <see cref="Editor"/> and the attachment state.
    /// </summary>
    private void UpdateEditorSubscription()
    {
        // The palette listens only while it is in the visual tree, so it never stays alive through a longer-lived editor
        var wanted = _isAttached ? Editor : null;
        if (!ReferenceEquals(wanted, _followedEditor))
        {
            if (_followedEditor is not null)
            {
                _followedEditor.PropertyChanged -= OnEditorPropertyChanged;
            }

            _followedEditor = wanted;
            if (_followedEditor is not null)
            {
                _followedEditor.PropertyChanged += OnEditorPropertyChanged;
            }
        }

        UpdateDiagramSubscription();
    }

    /// <summary>
    ///     Makes the subscription to the followed editor's diagram match the editor's current page.
    /// </summary>
    /// <returns><see langword="true"/> when the palette now follows a different diagram than before.</returns>
    private bool UpdateDiagramSubscription()
    {
        var wanted = _followedEditor?.Page?.Diagram;
        if (ReferenceEquals(wanted, _followedDiagram))
        {
            return false;
        }

        if (_followedDiagram is not null)
        {
            _followedDiagram.Changed -= OnFollowedDiagramChanged;
        }

        _followedDiagram = wanted;
        if (_followedDiagram is not null)
        {
            _followedDiagram.Changed += OnFollowedDiagramChanged;
        }

        return true;
    }

    /// <summary>
    ///     Follows the editor: a different diagram brings its types, a different theme its look.
    /// </summary>
    /// <param name="sender">The editor.</param>
    /// <param name="e">The property change.</param>
    private void OnEditorPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == DiagramEditor.PageProperty)
        {
            // Switching pages of the same diagram changes nothing the palette shows, so its scroll position is kept
            if (UpdateDiagramSubscription())
            {
                MarkItemsDirty();
                InvalidateVisual();
            }
        }
        else if (e.Property == DiagramEditor.DiagramThemeProperty)
        {
            MarkItemsDirty();
            InvalidateVisual();
        }
    }

    /// <summary>
    ///     Redraws the palette when the followed diagram's theme is replaced.
    /// </summary>
    /// <param name="sender">The diagram.</param>
    /// <param name="e">The change description.</param>
    private void OnFollowedDiagramChanged(object? sender, DiagramChangedEventArgs e)
    {
        if (e.Kind == DiagramChangeKind.SettingsChanged && e.PropertyName == nameof(Diagram.Theme))
        {
            MarkItemsDirty();
            InvalidateVisual();
        }
    }

    /// <summary>
    ///     Copies what the user types in the search box to <see cref="Filter"/>.
    /// </summary>
    /// <param name="sender">The search box.</param>
    /// <param name="e">The property change.</param>
    private void OnFilterBoxPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == TextBox.TextProperty && !string.Equals(_filterBox.Text ?? string.Empty, Filter ?? string.Empty, StringComparison.Ordinal))
        {
            Filter = _filterBox.Text;
        }
    }

    /// <summary>
    ///     Handles the palette keys: Up, Down, Left, Right, Home and End move the selection through the visible items (by row and column), and Enter activates the selected type like a double-click (raises <see cref="NodeTypeActivated"/>). Protected override of the Avalonia hook.
    /// </summary>
    /// <param name="e">The key event supplied by Avalonia.</param>
    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        ArgumentNullException.ThrowIfNull(e);
        EnsureItems();

        // Moving by rows needs to know where the items are, so bring the layout up to date first
        UpdateLayout();
        if (_items.Count == 0 || e.KeyModifiers != KeyModifiers.None)
        {
            return;
        }

        // In the search box the editing keys belong to the box; only moving through the results and Enter reach the list
        var fromFilterBox = e.Source is Visual source && source.GetSelfAndVisualAncestors().Contains(_filterBox);
        if (fromFilterBox && e.Key is not (Key.Down or Key.Up or Key.Enter))
        {
            return;
        }

        var current = _items.FindIndex(item => item.IsSelected);
        switch (e.Key)
        {
            case Key.Down:
                SelectIndex(current < 0 ? 0 : MoveVertically(current, 1));
                break;
            case Key.Up:
                SelectIndex(current < 0 ? _items.Count - 1 : MoveVertically(current, -1));
                break;
            case Key.Right or Key.Left when _stack.ColumnCount > 1 && _stack.Cells.Count == _items.Count:
                SelectIndex(MoveHorizontally(current, e.Key == Key.Right ? 1 : -1));
                break;
            case Key.Home:
                SelectIndex(0);
                break;
            case Key.End:
                SelectIndex(_items.Count - 1);
                break;
            case Key.Enter when current >= 0:
                Activate(_items[current].NodeType);
                break;
            default:
                return;
        }

        e.Handled = true;
    }

    /// <summary>
    ///     Finds the item one step to the left or right of an item, staying in its row.
    /// </summary>
    /// <param name="current">The index of the current item.</param>
    /// <param name="step">Minus one for the left neighbor, one for the right neighbor.</param>
    /// <returns>The index of the neighbor, or <paramref name="current"/> when the row ends there.</returns>
    private int MoveHorizontally(int current, int step)
    {
        if (current < 0)
        {
            // Nothing is selected yet: start at the first item going right, at the last going left
            return step > 0 ? 0 : _items.Count - 1;
        }

        var cells = _stack.Cells;
        var next = current + step;
        return next >= 0 && next < _items.Count && cells[next].Row == cells[current].Row ? next : current;
    }

    /// <summary>
    ///     Finds the item one row above or below an item, in the same column or the nearest column that row has.
    /// </summary>
    /// <remarks>
    ///     A header does not stop the movement: from the last row of a section the next row is the first row of the
    ///     following section. In a single column this is simply the previous or next item.
    /// </remarks>
    /// <param name="current">The index of the current item.</param>
    /// <param name="step">Minus one for the row above, one for the row below.</param>
    /// <returns>The index of the item in that row, or <paramref name="current"/> when there is no such row.</returns>
    private int MoveVertically(int current, int step)
    {
        var cells = _stack.Cells;
        if (cells.Count != _items.Count)
        {
            // Not laid out (the palette is not shown): one item per row
            return Math.Clamp(current + step, 0, _items.Count - 1);
        }

        var (row, column) = cells[current];

        // The target row holds the items with that row number; the last one that starts at or before the column is the nearest
        var target = -1;
        for (var i = 0; i < cells.Count; i++)
        {
            if (cells[i].Row == row + step && cells[i].Column <= column)
            {
                target = i;
            }
        }

        return target < 0 ? current : target;
    }

    /// <summary>
    ///     Selects the item at an index and scrolls it into view.
    /// </summary>
    /// <param name="index">The index into the shown items.</param>
    private void SelectIndex(int index)
    {
        var item = _items[index];
        SelectedNodeType = item.NodeType;
        item.BringIntoView();
    }
}
