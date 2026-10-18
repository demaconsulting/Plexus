using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.VisualTree;
using global::Avalonia.Automation;
using AvaloniaGeometry = Avalonia.Media.Geometry;
using ShapePath = Avalonia.Controls.Shapes.Path;

namespace DemaConsulting.Plexus.Avalonia.PropertyGrid;

/// <summary>
///     One visual row of a <see cref="PropertyGrid"/>: either a category band or a property row.
/// </summary>
internal abstract class GridRowView : Border
{
    /// <summary>
    ///     Gets the key that identifies the row within its grid (<c>c:Category</c> or <c>p:Path</c>).
    /// </summary>
    internal string Key { get; private protected set; } = string.Empty;

    /// <summary>
    ///     Gets the item of a property row, or <see langword="null"/> for a category row.
    /// </summary>
    internal virtual PropertyItem? Item => null;

    /// <summary>
    ///     Shows or hides the selection highlight.
    /// </summary>
    /// <param name="selected">Whether the row is selected.</param>
    internal abstract void SetSelected(bool selected);

    /// <summary>
    ///     Creates the expander glyph.
    /// </summary>
    /// <param name="expanded">Whether the glyph shows the expanded state.</param>
    /// <returns>The glyph.</returns>
    protected static ShapePath CreateChevron(bool expanded)
    {
        var chevron = new ShapePath
        {
            Stretch = Stretch.None,
            StrokeThickness = 1.3,
            StrokeLineCap = PenLineCap.Round,
            StrokeJoin = PenLineJoin.Round,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
        };
        GridTheme.BindBrush(chevron, Shape.StrokeProperty, GridTheme.SecondaryTextKey);
        SetChevron(chevron, expanded);
        return chevron;
    }

    /// <summary>
    ///     Points a glyph right (collapsed) or down (expanded).
    /// </summary>
    /// <param name="chevron">The glyph.</param>
    /// <param name="expanded">Whether the row is expanded.</param>
    protected static void SetChevron(ShapePath chevron, bool expanded) =>
        chevron.Data = AvaloniaGeometry.Parse(expanded ? "M0,2 L4,6 L8,2" : "M2,0 L6,4 L2,8");
}

/// <summary>
///     The band that heads a category: an expander and the category name.
/// </summary>
internal sealed class CategoryRowView : GridRowView
{
    /// <summary>
    ///     The category name.
    /// </summary>
    private readonly string _category;

    /// <summary>
    ///     The expander glyph.
    /// </summary>
    private readonly ShapePath _chevron;

    /// <summary>
    ///     The category name text.
    /// </summary>
    private readonly TextBlock _label;

    /// <summary>
    ///     Initializes a new instance of the <see cref="CategoryRowView"/> class.
    /// </summary>
    /// <param name="owner">The grid that hosts the row.</param>
    /// <param name="category">The category name.</param>
    /// <param name="expanded">Whether the category is expanded.</param>
    internal CategoryRowView(PropertyGrid owner, string category, bool expanded)
    {
        _category = category;
        Key = PropertyGrid.CategoryKey(category);
        Height = GridTheme.RowHeight;
        BorderThickness = new Thickness(0, 0, 0, 1);
        GridTheme.BindBrush(this, BorderBrushProperty, GridTheme.LineKey);
        _chevron = CreateChevron(expanded);
        var expander = new Border
        {
            Width = GridTheme.ExpanderWidth,
            Background = Brushes.Transparent,
            Child = _chevron,
        };
        expander.PointerPressed += (_, e) =>
        {
            owner.SelectKey(Key, true);
            owner.SetCategoryExpanded(category, !owner.IsCategoryExpanded(category));
            e.Handled = true;
        };
        _label = new TextBlock
        {
            Text = category,
            FontWeight = FontWeight.SemiBold,
            FontSize = GridTheme.FontSize,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(2, 0, 0, 0),
        };
        var panel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(2, 0, 0, 0) };
        panel.Children.Add(expander);
        panel.Children.Add(_label);
        Child = panel;
        AutomationProperties.SetAutomationId(this, "PropertyGrid_Category_" + category);
        AutomationProperties.SetName(this, category);
        PointerPressed += (_, e) =>
        {
            owner.SelectKey(Key, true);
            if (e.ClickCount == 2)
            {
                owner.SetCategoryExpanded(category, !owner.IsCategoryExpanded(category));
            }
        };
        SetSelected(false);
    }

    /// <summary>
    ///     Updates the expander state.
    /// </summary>
    /// <param name="expanded">Whether the category is expanded.</param>
    internal void Update(bool expanded) => SetChevron(_chevron, expanded);

    /// <inheritdoc />
    internal override void SetSelected(bool selected)
    {
        if (selected)
        {
            GridTheme.BindBrush(this, BackgroundProperty, GridTheme.SelectionKey);
            _label.Foreground = GridTheme.SelectedTextBrush;
        }
        else
        {
            GridTheme.BindBrush(this, BackgroundProperty, GridTheme.BandKey);
            _label.ClearValue(TextBlock.ForegroundProperty);
        }
    }

    /// <summary>
    ///     Gets the category name.
    /// </summary>
    internal string Category => _category;
}

/// <summary>
///     A property row: the name cell (indent, expander, label), a one pixel column line and the value cell that hosts
///     the editor.
/// </summary>
internal sealed class PropertyRowView : GridRowView
{
    /// <summary>
    ///     The grid that hosts the row.
    /// </summary>
    private readonly PropertyGrid _owner;

    /// <summary>
    ///     The item the row shows.
    /// </summary>
    private readonly PropertyItem _item;

    /// <summary>
    ///     The row's layout grid (name, line, value).
    /// </summary>
    private readonly Grid _layout;

    /// <summary>
    ///     The name cell.
    /// </summary>
    private readonly Border _nameCell;

    /// <summary>
    ///     The name text.
    /// </summary>
    private readonly TextBlock _label;

    /// <summary>
    ///     The expander glyph.
    /// </summary>
    private readonly ShapePath _chevron;

    /// <summary>
    ///     The hit box of the expander.
    /// </summary>
    private readonly Border _expander;

    /// <summary>
    ///     The value cell (carries the error border).
    /// </summary>
    private readonly Border _valueHost;

    /// <summary>
    ///     The nesting depth.
    /// </summary>
    private int _depth;

    /// <summary>
    ///     Whether the row has an expander.
    /// </summary>
    private bool _expandable;

    /// <summary>
    ///     Initializes a new instance of the <see cref="PropertyRowView"/> class.
    /// </summary>
    /// <param name="owner">The grid that hosts the row.</param>
    /// <param name="item">The item to show.</param>
    internal PropertyRowView(PropertyGrid owner, PropertyItem item)
    {
        _owner = owner;
        _item = item;
        Key = PropertyGrid.PropertyKey(item.Path);
        Context = new PropertyEditorContext(owner, item);
        Height = GridTheme.RowHeight;
        BorderThickness = new Thickness(0, 0, 0, 1);
        GridTheme.BindBrush(this, BorderBrushProperty, GridTheme.LineKey);
        _layout = new Grid { ColumnDefinitions = [new(owner.NameColumnWidth, GridUnitType.Pixel), new(1, GridUnitType.Pixel), new(1, GridUnitType.Star)] };

        _chevron = CreateChevron(false);
        _expander = new Border { Width = GridTheme.ExpanderWidth, Background = Brushes.Transparent, Child = _chevron, IsVisible = false };
        _expander.PointerPressed += (_, e) =>
        {
            owner.SelectKey(Key, true);
            owner.SetExpanded(item.Path, !owner.IsExpanded(item.Path));
            e.Handled = true;
        };
        _label = new TextBlock
        {
            FontSize = GridTheme.FontSize,
            VerticalAlignment = VerticalAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis,
        };
        var namePanel = new DockPanel();
        DockPanel.SetDock(_expander, Dock.Left);
        namePanel.Children.Add(_expander);
        namePanel.Children.Add(_label);
        _nameCell = new Border { Child = namePanel, Padding = new Thickness(4, 0, 4, 0) };
        _layout.Children.Add(_nameCell);

        var line = new Border();
        GridTheme.BindBrush(line, BackgroundProperty, GridTheme.LineKey);
        Grid.SetColumn(line, 1);
        _layout.Children.Add(line);

        var editor = owner.CreateEditor(Context);
        _valueHost = new Border { BorderThickness = new Thickness(1), Child = editor, ClipToBounds = true };
        Grid.SetColumn(_valueHost, 2);
        _layout.Children.Add(_valueHost);
        Child = _layout;

        var id = "PropertyGrid_Row_" + item.Path;
        AutomationProperties.SetAutomationId(this, id);
        AutomationProperties.SetName(this, item.Label);
        AutomationProperties.SetAutomationId(editor, "PropertyGrid_Editor_" + item.Path);
        AutomationProperties.SetName(editor, item.Label);

        PointerPressed += OnPointerPressed;
        _valueHost.AddHandler(GotFocusEvent, (_, _) => owner.SelectKey(Key, false), RoutingStrategies.Bubble, true);
        ContextMenu = CreateMenu();
        SetSelected(false);
    }

    /// <summary>
    ///     Gets the editor context of the row.
    /// </summary>
    internal PropertyEditorContext Context { get; }

    /// <inheritdoc />
    internal override PropertyItem? Item => _item;

    /// <summary>
    ///     Gets the name text.
    /// </summary>
    internal TextBlock NameLabel => _label;

    /// <summary>
    ///     Gets the current width of the name column in pixels.
    /// </summary>
    internal double NameWidth => _layout.ColumnDefinitions[0].Width.Value;

    /// <summary>
    ///     Gets the value cell, which carries the error border and tooltip.
    /// </summary>
    internal Border ValueHost => _valueHost;

    /// <summary>
    ///     Gets the editor control of the row.
    /// </summary>
    internal Control Editor => _valueHost.Child!;

    /// <summary>
    ///     Applies the row's structure and shows the current state of the item.
    /// </summary>
    /// <param name="depth">The nesting depth.</param>
    /// <param name="expandable">Whether the row can be expanded.</param>
    /// <param name="expanded">Whether it is expanded.</param>
    internal void Update(int depth, bool expandable, bool expanded)
    {
        _depth = depth;
        _expandable = expandable;
        _nameCell.Padding = new Thickness(4 + (depth * GridTheme.IndentWidth), 0, 4, 0);
        _expander.IsVisible = expandable;
        SetChevron(_chevron, expanded);
        Refresh();
    }

    /// <summary>
    ///     Shows the current label, state (changed, read-only, error) and value of the item.
    /// </summary>
    internal void Refresh()
    {
        _label.Text = _item.Label;
        _label.Opacity = _item.IsReadOnly ? 0.6 : 1;
        TextElement.SetFontWeight(_valueHost, _owner.IsChanged(_item) ? FontWeight.Bold : FontWeight.Normal);
        var error = _owner.Session?.GetError(_item);
        _valueHost.BorderBrush = error is null ? Brushes.Transparent : GridTheme.ErrorBrush;
        _valueHost.Background = error is null ? null : GridTheme.ErrorTint;
        ToolTip.SetTip(_valueHost, error);
        Context.RaiseRefreshed();
    }

    /// <summary>
    ///     Sets the width of the name column.
    /// </summary>
    /// <param name="width">The width in pixels.</param>
    internal void SetNameWidth(double width) => _layout.ColumnDefinitions[0].Width = new GridLength(width, GridUnitType.Pixel);

    /// <summary>
    ///     Moves keyboard focus to the editor.
    /// </summary>
    /// <returns><see langword="true"/> when an element took the focus.</returns>
    internal bool FocusEditor() => PropertyGrid.FocusFirst(Editor);

    /// <inheritdoc />
    internal override void SetSelected(bool selected)
    {
        if (selected)
        {
            GridTheme.BindBrush(_nameCell, BackgroundProperty, GridTheme.SelectionKey);
            _label.Foreground = GridTheme.SelectedTextBrush;
        }
        else
        {
            _nameCell.ClearValue(BackgroundProperty);
            _label.ClearValue(TextBlock.ForegroundProperty);
        }
    }

    /// <summary>
    ///     Gets the nesting depth.
    /// </summary>
    internal int Depth => _depth;

    /// <summary>
    ///     Gets a value indicating whether the row shows an expander.
    /// </summary>
    internal bool IsExpandable => _expandable;

    /// <summary>
    ///     Selects the row when the pointer is pressed on it.
    /// </summary>
    /// <param name="sender">The row.</param>
    /// <param name="e">The event data.</param>
    private void OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        var onName = e.Source is Visual source && (ReferenceEquals(source, _nameCell) || _nameCell.IsVisualAncestorOf(source));
        _owner.SelectKey(Key, onName);
        if (onName && e.ClickCount == 2 && _expandable)
        {
            _owner.SetExpanded(_item.Path, !_owner.IsExpanded(_item.Path));
        }
    }

    /// <summary>
    ///     Creates the context menu of the row (reset to default).
    /// </summary>
    /// <returns>The menu.</returns>
    private ContextMenu CreateMenu()
    {
        var reset = new MenuItem { Header = "Reset" };
        reset.Click += (_, _) => _owner.Session?.Reset(_item);
        var menu = new ContextMenu();
        menu.Items.Add(reset);
        menu.Opening += (_, _) => reset.IsEnabled = _item.CanReset;
        return menu;
    }
}
