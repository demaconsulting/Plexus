using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.VisualTree;

namespace DemaConsulting.Plexus.Avalonia.PropertyGrid;

/// <content>
///     Keyboard navigation and editing.
/// </content>
public partial class PropertyGrid
{
    /// <summary>
    ///     The number of rows Page Up and Page Down move.
    /// </summary>
    private const int PageSize = 10;

    /// <summary>
    ///     Handles keys before the focused element sees them: row navigation while the grid itself has the focus (the
    ///     name side of a row), and commit, cancel and tabbing while an editor has it.
    /// </summary>
    /// <param name="sender">The grid.</param>
    /// <param name="e">The event data.</param>
    private void OnKeyDownTunnel(object? sender, KeyEventArgs e)
    {
        var onGrid = ReferenceEquals(e.Source, this);
        var inRows = !onGrid && e.Source is Visual visual && _scroll.IsVisualAncestorOf(visual);
        if (!onGrid && !inRows)
        {
            return;
        }

        e.Handled = inRows ? HandleEditorKey(e) : HandleRowKey(e);
    }

    /// <summary>
    ///     Handles a key pressed while an editor has the focus.
    /// </summary>
    /// <param name="e">The event data.</param>
    /// <returns><see langword="true"/> when the key was handled.</returns>
    private bool HandleEditorKey(KeyEventArgs e)
    {
        var inText = e.Source is TextBox;
        var shift = e.KeyModifiers.HasFlag(KeyModifiers.Shift);
        switch (e.Key)
        {
            case Key.Enter when inText:
                Focus();
                SelectedView()?.Context.RaiseCommitRequested();
                return true;
            case Key.Escape when inText || SelectedView() is { Context.PendingText: not null }:
                SelectedView()?.Context.RaiseCancelRequested();
                Focus();
                return true;
            case Key.Tab when shift:
                Focus();
                return true;
            case Key.Tab:
                return TabToNextRow();
            default:
                return false;
        }
    }

    /// <summary>
    ///     Handles a key pressed while the grid itself (the name side of a row) has the focus.
    /// </summary>
    /// <param name="e">The event data.</param>
    /// <returns><see langword="true"/> when the key was handled.</returns>
    private bool HandleRowKey(KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Down:
                return MoveSelection(1);
            case Key.Up:
                return MoveSelection(-1);
            case Key.PageDown:
                return MoveSelection(PageSize);
            case Key.PageUp:
                return MoveSelection(-PageSize);
            case Key.Home:
                return SelectAt(0);
            case Key.End:
                return SelectAt(_rowsPanel.Children.Count - 1);
            case Key.Left:
                return CollapseOrSelectParent();
            case Key.Right:
                return ExpandOrSelectChild();
            case Key.Enter:
            case Key.F2:
                return BeginEdit();
            case Key.Escape:
                return CancelSelectedEdit();
            case Key.Tab when e.KeyModifiers.HasFlag(KeyModifiers.Shift):
                return TabToPreviousEditor();
            case Key.Tab:
                return SelectedView() is { } view && view.FocusEditor();
            default:
                return false;
        }
    }

    /// <summary>
    ///     Gets the view of the selected property row.
    /// </summary>
    /// <returns>The view, or <see langword="null"/> when no property row is selected.</returns>
    private PropertyRowView? SelectedView() =>
        _selectedKey is not null && _views.TryGetValue(_selectedKey, out var view) ? view as PropertyRowView : null;

    /// <summary>
    ///     Starts editing the selected row: moves the focus to its editor, or toggles a category or nested object.
    /// </summary>
    /// <returns><see langword="true"/> when something happened.</returns>
    private bool BeginEdit()
    {
        if (_selectedKey is null || !_views.TryGetValue(_selectedKey, out var view))
        {
            return false;
        }

        switch (view)
        {
            case CategoryRowView category:
                SetCategoryExpanded(category.Category, !IsCategoryExpanded(category.Category));
                return true;
            case PropertyRowView property when property.FocusEditor():
                return true;
            case PropertyRowView { IsExpandable: true } property:
                SetExpanded(property.Item!.Path, !IsExpanded(property.Item.Path));
                return true;
            default:
                return false;
        }
    }

    /// <summary>
    ///     Abandons a rejected edit (invalid text and its error) of the selected row.
    /// </summary>
    /// <returns><see langword="true"/> when the row had a rejected edit.</returns>
    private bool CancelSelectedEdit()
    {
        if (SelectedView() is { Item: { } item } view && (view.Context.PendingText is not null || Session?.GetError(item) is not null))
        {
            view.Context.RaiseCancelRequested();
            return true;
        }

        return false;
    }

    /// <summary>
    ///     Moves the selection by a number of rows.
    /// </summary>
    /// <param name="delta">The number of rows (negative moves up).</param>
    /// <returns><see langword="true"/> when there are rows.</returns>
    private bool MoveSelection(int delta)
    {
        var rows = _rowsPanel.Children;
        if (rows.Count == 0)
        {
            return false;
        }

        var current = _selectedKey is null ? -1 : rows.ToList().FindIndex(r => r is GridRowView v && v.Key == _selectedKey);
        var fallback = delta > 0 ? 0 : rows.Count - 1;
        var target = current < 0 ? fallback : Math.Clamp(current + delta, 0, rows.Count - 1);
        return SelectAt(target);
    }

    /// <summary>
    ///     Selects the row at an index and scrolls it into view.
    /// </summary>
    /// <param name="index">The index.</param>
    /// <returns><see langword="true"/> when the index is valid.</returns>
    private bool SelectAt(int index)
    {
        if (index < 0 || index >= _rowsPanel.Children.Count || _rowsPanel.Children[index] is not GridRowView view)
        {
            return false;
        }

        SelectKey(view.Key, false);
        view.BringIntoView();
        return true;
    }

    /// <summary>
    ///     Collapses the selected row, or selects its parent when it is already collapsed.
    /// </summary>
    /// <returns><see langword="true"/> when something happened.</returns>
    private bool CollapseOrSelectParent()
    {
        switch (SelectedViewAny())
        {
            case CategoryRowView category when IsCategoryExpanded(category.Category):
                SetCategoryExpanded(category.Category, false);
                return true;
            case PropertyRowView { Item: { } item } when IsExpanded(item.Path):
                SetExpanded(item.Path, false);
                return true;
            case PropertyRowView { Item: { Parent: { } parent } }:
                return SelectKeyAndShow(PropertyKey(parent.Path));
            case PropertyRowView { Item: { } top } when SortMode == PropertySortMode.Categorized:
                return SelectKeyAndShow(CategoryKey(top.Category));
            default:
                return false;
        }
    }

    /// <summary>
    ///     Expands the selected row, or moves to its first child when it is already expanded.
    /// </summary>
    /// <returns><see langword="true"/> when something happened.</returns>
    private bool ExpandOrSelectChild()
    {
        switch (SelectedViewAny())
        {
            case CategoryRowView category when !IsCategoryExpanded(category.Category):
                SetCategoryExpanded(category.Category, true);
                return true;
            case PropertyRowView { IsExpandable: true, Item: { } item } when !IsExpanded(item.Path):
                SetExpanded(item.Path, true);
                return true;
            case not null:
                return MoveSelection(1);
            default:
                return false;
        }
    }

    /// <summary>
    ///     Gets the view of the selected row, whatever its kind.
    /// </summary>
    /// <returns>The view, or <see langword="null"/> when nothing is selected.</returns>
    private GridRowView? SelectedViewAny() =>
        _selectedKey is not null && _views.TryGetValue(_selectedKey, out var view) ? view : null;

    /// <summary>
    ///     Selects a row by key and scrolls it into view.
    /// </summary>
    /// <param name="key">The row key.</param>
    /// <returns><see langword="true"/> when the row exists.</returns>
    private bool SelectKeyAndShow(string key)
    {
        if (!_views.TryGetValue(key, out var view))
        {
            return false;
        }

        SelectKey(key, false);
        view.BringIntoView();
        return true;
    }

    /// <summary>
    ///     Leaves the editor of the selected row forward: selects the next row and puts the focus on its name.
    /// </summary>
    /// <returns><see langword="true"/> when there was a next row; otherwise the key is left to normal tabbing.</returns>
    private bool TabToNextRow()
    {
        var current = _selectedKey is null ? -1 : _rowsPanel.Children.ToList().FindIndex(r => r is GridRowView v && v.Key == _selectedKey);
        if (current < 0 || current >= _rowsPanel.Children.Count - 1)
        {
            return false;
        }

        Focus();
        return SelectAt(current + 1);
    }

    /// <summary>
    ///     Moves back from the name of the selected row to the editor of the previous property row.
    /// </summary>
    /// <returns><see langword="true"/> when an editor took the focus.</returns>
    private bool TabToPreviousEditor()
    {
        var current = _selectedKey is null ? -1 : _rowsPanel.Children.ToList().FindIndex(r => r is GridRowView v && v.Key == _selectedKey);
        for (var index = current - 1; index >= 0; index--)
        {
            if (_rowsPanel.Children[index] is PropertyRowView view && view.Item is not null)
            {
                SelectKey(view.Key, false);
                if (view.FocusEditor())
                {
                    return true;
                }
            }
        }

        return false;
    }
}
