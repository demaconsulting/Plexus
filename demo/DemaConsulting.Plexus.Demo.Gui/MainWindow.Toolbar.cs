using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Markup.Xaml.MarkupExtensions;
using Avalonia.Media;
using Avalonia.Styling;
using Material.Icons;
using Material.Icons.Avalonia;

namespace DemaConsulting.Plexus.Demo.Gui;

/// <content>
///     The toolbar under the menu bar: an icon button per frequent command, each one a mirror of its menu item.
/// </content>
/// <remarks>
///     A button owns no behavior. Pressing it raises the click of its menu item (or flips the item's check mark), so the
///     menu item's handler is the one implementation of the command, and the button copies the item's enabled and
///     checked state and builds its tooltip from the item's caption and shortcut.
/// </remarks>
internal sealed partial class MainWindow
{
    /// <summary>
    ///     The size of a toolbar button, in device-independent pixels.
    /// </summary>
    private const double ToolbarButtonSize = 28;

    /// <summary>
    ///     The size of a toolbar icon, in device-independent pixels.
    /// </summary>
    private const double ToolbarIconSize = 18;

    /// <summary>
    ///     The toolbar groups, in order: the menu path (after <c>Menu_</c>) of each command, its icon and whether it is a toggle.
    /// </summary>
    /// <remarks>
    ///     The button's AutomationId is <c>Toolbar_</c> followed by the part of the menu path after the first underscore
    ///     (<c>Edit_Undo</c> gives <c>Toolbar_Undo</c>).
    /// </remarks>
    private static readonly ToolbarEntry[][] ToolbarGroups =
    [
        [
            new("File_New", MaterialIconKind.FilePlusOutline),
            new("File_Open", MaterialIconKind.FolderOpenOutline),
            new("File_Save", MaterialIconKind.ContentSave),
        ],
        [
            new("Edit_Undo", MaterialIconKind.Undo),
            new("Edit_Redo", MaterialIconKind.Redo),
        ],
        [
            new("Edit_Cut", MaterialIconKind.ContentCut),
            new("Edit_Copy", MaterialIconKind.ContentCopy),
            new("Edit_Paste", MaterialIconKind.ContentPaste),
            new("Edit_Delete", MaterialIconKind.DeleteOutline),
        ],
        [
            new("Insert_Group", MaterialIconKind.Group),
            new("Insert_Ungroup", MaterialIconKind.Ungroup),
            new("Insert_ToggleCollapse", MaterialIconKind.ArrowCollapseVertical),
            new("Insert_Annotation", MaterialIconKind.NoteTextOutline, Label: "Add Annotation"),
        ],
        [
            new("View_ZoomIn", MaterialIconKind.MagnifyPlusOutline),
            new("View_ZoomOut", MaterialIconKind.MagnifyMinusOutline),
            new("View_ZoomToFit", MaterialIconKind.FitToPageOutline),
            new("View_ShowGrid", MaterialIconKind.Grid, IsToggle: true),
            new("View_SnapToGrid", MaterialIconKind.Magnet, IsToggle: true),
        ],
        [
            new("Edit_Properties", MaterialIconKind.TuneVariant),
            new("View_PropertiesPanel", MaterialIconKind.DockRight, IsToggle: true),
        ],
        [
            new("File_ExportPng", MaterialIconKind.ImageOutline),
        ],
    ];

    /// <summary>
    ///     The toolbar buttons with the menu items they mirror and the tooltip names that are not the menu captions.
    /// </summary>
    private readonly List<(MenuItem Item, Button Button, string? Label)> _toolbarButtons = [];

    /// <summary>
    ///     The Show Toolbar menu item.
    /// </summary>
    private MenuItem _showToolbar = new();

    /// <summary>
    ///     The toolbar (a border around the row of buttons), hidden by View &gt; Show Toolbar.
    /// </summary>
    private Border _toolbar = new();

    /// <summary>
    ///     Builds the Show Toolbar item of the View menu.
    /// </summary>
    /// <returns>The menu item, checked because the toolbar starts visible.</returns>
    private MenuItem BuildShowToolbarItem()
    {
        _showToolbar = Ui.Item("Show _Toolbar", AutomationIds.Menu("View_ShowToolbar"));
        _showToolbar.ToggleType = MenuItemToggleType.CheckBox;
        _showToolbar.IsChecked = true;
        _showToolbar.PropertyChanged += (_, e) =>
        {
            if (e.Property == MenuItem.IsCheckedProperty)
            {
                _toolbar.IsVisible = _showToolbar.IsChecked;
            }
        };
        return _showToolbar;
    }

    /// <summary>
    ///     Builds the toolbar. The menu bar must exist already, because the buttons mirror its items.
    /// </summary>
    /// <returns>The toolbar.</returns>
    private Border BuildToolbar()
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 2, Margin = new Thickness(4, 3) };
        foreach (var group in ToolbarGroups)
        {
            if (row.Children.Count > 0)
            {
                row.Children.Add(new Border
                {
                    Width = 1,
                    Height = ToolbarIconSize,
                    Margin = new Thickness(5, 0),
                    VerticalAlignment = VerticalAlignment.Center,
                    Background = new SolidColorBrush(Colors.Gray, 0.6),
                });
            }

            foreach (var entry in group)
            {
                row.Children.Add(BuildToolbarButton(entry));
            }
        }

        // A disabled command's icon is dimmed, the way a disabled menu item's caption is; the theme's own grey disabled
        // background and the accent fill of a checked toggle are replaced by a flat look: transparent, or a soft tint when on
        var dimmed = new Style(x => x.OfType<Button>().Class(":disabled").Descendant().OfType<MaterialIcon>());
        dimmed.Setters.Add(new Setter(OpacityProperty, 0.35));
        var flat = new Style(x => x.OfType<Button>().Class(":disabled").Template().OfType<ContentPresenter>().Name("PART_ContentPresenter"));
        flat.Setters.Add(new Setter(ContentPresenter.BackgroundProperty, Brushes.Transparent));
        flat.Setters.Add(new Setter(ContentPresenter.BorderBrushProperty, Brushes.Transparent));
        var tint = new SolidColorBrush(Colors.Gray, 0.4);
        var on = new Style(x => x.OfType<ToggleButton>().Class(":checked").Template().OfType<ContentPresenter>().Name("PART_ContentPresenter"));
        on.Setters.Add(new Setter(ContentPresenter.BackgroundProperty, tint));
        on.Setters.Add(new Setter(ContentPresenter.BorderBrushProperty, tint));
        on.Setters.Add(new Setter(TextElement.ForegroundProperty, new DynamicResourceExtension("ButtonForeground")));

        _toolbar = new Border
        {
            BorderBrush = Brushes.Gray,
            BorderThickness = new Thickness(0, 0, 0, 1),
            Child = row,
            Styles = { dimmed, flat, on },
        }.WithId(AutomationIds.Toolbar);
        RefreshToolbarTips();
        return _toolbar;
    }

    /// <summary>
    ///     Builds the button of one command, wired to the menu item of the same command.
    /// </summary>
    /// <param name="entry">The command.</param>
    /// <returns>The button.</returns>
    private Button BuildToolbarButton(ToolbarEntry entry)
    {
        var item = FindMenuItem(entry.MenuPath);
        Button button = entry.IsToggle ? new ToggleButton() : new Button();
        button.Content = new MaterialIcon { Kind = entry.Kind, Width = ToolbarIconSize, Height = ToolbarIconSize };
        button.Width = ToolbarButtonSize;
        button.Height = ToolbarButtonSize;
        button.Padding = new Thickness(0);
        button.CornerRadius = new CornerRadius(4);
        button.BorderThickness = new Thickness(0);
        button.Background = Brushes.Transparent;
        button.HorizontalContentAlignment = HorizontalAlignment.Center;
        button.VerticalContentAlignment = VerticalAlignment.Center;

        // Pressing a button must not take the keyboard focus from the canvas, or Cut, Copy, Paste and Delete would lose their target
        button.Focusable = false;
        button.WithId(AutomationIds.ToolbarButton(entry.MenuPath[(entry.MenuPath.IndexOf('_', StringComparison.Ordinal) + 1)..]));

        // The button runs the menu item's own handler: a command raises the item's click, a toggle flips the item's check mark
        // (whose change handler applies it), and the item's state flows back into the button
        button.Click += (_, _) =>
        {
            if (entry.IsToggle)
            {
                item.IsChecked = !item.IsChecked;
            }
            else
            {
                item.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
            }
        };

        void Mirror()
        {
            button.IsEnabled = item.IsEnabled;
            if (button is ToggleButton toggle)
            {
                toggle.IsChecked = item.IsChecked;
            }
        }

        item.PropertyChanged += (_, e) =>
        {
            if (e.Property == InputElement.IsEnabledProperty || e.Property == MenuItem.IsCheckedProperty)
            {
                Mirror();
            }
        };
        Mirror();
        _toolbarButtons.Add((item, button, entry.Label));
        return button;
    }

    /// <summary>
    ///     Finds a menu item by its menu path.
    /// </summary>
    /// <param name="path">The path after <c>Menu_</c>, for example <c>File_Save</c>.</param>
    /// <returns>The item.</returns>
    private MenuItem FindMenuItem(string path)
    {
        var id = AutomationIds.Menu(path);
        return AllMenuItems(_menuBar.Items).FirstOrDefault(item => AutomationProperties.GetAutomationId(item) == id)
            ?? throw new InvalidOperationException($"There is no menu item {id}.");
    }

    /// <summary>
    ///     Lists the menu items of a menu and all of its submenus.
    /// </summary>
    /// <param name="items">The items of a menu or menu bar.</param>
    /// <returns>Every menu item, parents first.</returns>
    private static IEnumerable<MenuItem> AllMenuItems(ItemCollection items)
    {
        foreach (var item in items.OfType<MenuItem>())
        {
            yield return item;
            foreach (var child in AllMenuItems(item.Items))
            {
                yield return child;
            }
        }
    }

    /// <summary>
    ///     Rewrites every toolbar tooltip from its menu item's current caption and shortcut, so Undo and Redo name their action.
    /// </summary>
    private void RefreshToolbarTips()
    {
        foreach (var (item, button, label) in _toolbarButtons)
        {
            var name = label ?? (item.Header as string ?? string.Empty).Replace("_", string.Empty, StringComparison.Ordinal).Replace("...", string.Empty, StringComparison.Ordinal);
            var tip = item.InputGesture is { } gesture ? $"{name} ({FormatGesture(gesture)})" : name;
            ToolTip.SetTip(button, tip);
            AutomationProperties.SetName(button, name);
        }
    }

    /// <summary>
    ///     Writes a shortcut the way a user reads it: the key names of the platform's text form, with the names of the
    ///     keys that are not self-explanatory replaced by what is printed on them.
    /// </summary>
    /// <param name="gesture">The shortcut.</param>
    /// <returns>The text, for example <c>Ctrl+Z</c> or <c>Alt+Enter</c>.</returns>
    private static string FormatGesture(KeyGesture gesture)
    {
        var text = gesture.ToString();
        var split = text.LastIndexOf('+');
        var key = split < 0 ? text : text[(split + 1)..];
        var readable = key switch
        {
            "Return" => "Enter",
            "OemPlus" => "+",
            "OemMinus" => "-",
            "D0" => "0",
            _ => key,
        };
        return split < 0 ? readable : $"{text[..(split + 1)]}{readable}";
    }

    /// <summary>
    ///     One toolbar command.
    /// </summary>
    /// <param name="MenuPath">The path of the menu item the button mirrors, after <c>Menu_</c>.</param>
    /// <param name="Kind">The icon.</param>
    /// <param name="IsToggle">Whether the command is a check-mark toggle.</param>
    /// <param name="Label">The tooltip name, or null to use the menu item's caption.</param>
    private readonly record struct ToolbarEntry(string MenuPath, MaterialIconKind Kind, bool IsToggle = false, string? Label = null);
}
