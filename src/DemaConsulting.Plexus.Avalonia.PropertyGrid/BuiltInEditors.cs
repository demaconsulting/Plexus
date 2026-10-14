using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Styling;
using AvaloniaColor = Avalonia.Media.Color;
using DrawingColor = System.Drawing.Color;

namespace DemaConsulting.Plexus.Avalonia.PropertyGrid;

/// <summary>
///     The editors the property grid ships with, and the registry that maps property types to them.
/// </summary>
/// <remarks>
///     Editors are lightweight and always visible (the value column of a row is its editor), styled flat so the grid
///     reads like a table. All of them read through <see cref="PropertyEditorContext.Value"/> and write through
///     <see cref="PropertyEditorContext.SetValue"/> / <see cref="PropertyEditorContext.SetText"/>.
/// </remarks>
internal static class BuiltInEditors
{
    /// <summary>
    ///     The height of an editor control inside a row.
    /// </summary>
    private const double EditorHeight = GridTheme.RowHeight - 3;

    /// <summary>
    ///     The character that masks password text.
    /// </summary>
    private const char PasswordCharacter = '\u25CF';

    /// <summary>
    ///     Creates a registry that holds the built-in editors.
    /// </summary>
    /// <returns>The new registry; its fallback edits any type that converts to and from text.</returns>
    internal static PropertyEditorRegistry<PropertyEditorFactory> CreateRegistry()
    {
        var registry = new PropertyEditorRegistry<PropertyEditorFactory>();
        registry.Register(typeof(bool), CheckBoxEditor);
        registry.Register(typeof(Enum), EnumEditor);
        registry.Register(typeof(AvaloniaColor), ColorEditor);
        registry.Register(typeof(DrawingColor), ColorEditor);
        registry.Fallback = TextOrChoiceEditor;
        return registry;
    }

    /// <summary>
    ///     Chooses the editor of a row that has no registered editor of its own: a drop-down for converters with
    ///     standard values, a text box for anything that converts from text, else read-only text.
    /// </summary>
    /// <param name="context">The row context.</param>
    /// <returns>The editor.</returns>
    internal static Control TextOrChoiceEditor(PropertyEditorContext context)
    {
        var item = context.Item;
        if (item.HasStandardValues)
        {
            return item.StandardValuesExclusive ? ChoiceEditor(context) : EditableChoiceEditor(context);
        }

        return item.SupportsTextEditing ? TextEditor(context) : ReadOnlyEditor(context);
    }

    /// <summary>
    ///     Creates the flat, single-line text editor (masked for password text).
    /// </summary>
    /// <param name="context">The row context.</param>
    /// <returns>The editor.</returns>
    internal static Control TextEditor(PropertyEditorContext context)
    {
        var box = NewTextBox(context.PendingText ?? context.Text);
        if (context.Item.IsPasswordText)
        {
            box.PasswordChar = PasswordCharacter;
        }

        WireTextCommit(box, context, () => context.PendingText ?? context.Text, text => context.SetText(text));
        return box;
    }

    /// <summary>
    ///     Creates the editor for a value that cannot be changed: its text, dimmed.
    /// </summary>
    /// <param name="context">The row context.</param>
    /// <returns>The editor.</returns>
    internal static Control ReadOnlyEditor(PropertyEditorContext context)
    {
        var block = NewTextBlock();
        var password = context.Item.IsPasswordText;
        void Show()
        {
            var text = context.PendingText ?? context.Text;
            block.Text = password ? new string(PasswordCharacter, text.Length) : text;
            ToolTip.SetTip(block, password || text.Length == 0 ? null : text);
        }

        Show();
        context.Refreshed += (_, _) => Show();
        return block;
    }

    /// <summary>
    ///     Creates the check box editor of <see cref="bool"/> (three-state for <c>bool?</c>).
    /// </summary>
    /// <param name="context">The row context.</param>
    /// <returns>The editor.</returns>
    internal static Control CheckBoxEditor(PropertyEditorContext context)
    {
        var nullable = Nullable.GetUnderlyingType(context.Item.PropertyType) is not null;
        var box = new CheckBox
        {
            IsThreeState = nullable,
            MinHeight = 0,
            MinWidth = 0,
            Padding = new Thickness(0),
            VerticalAlignment = VerticalAlignment.Center,
            IsEnabled = !context.IsReadOnly,
        };
        var label = new TextBlock { FontSize = GridTheme.FontSize, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 0, 1) };
        var updating = false;
        void Show()
        {
            updating = true;
            box.IsChecked = context.Value as bool?;
            label.Text = context.Text;
            updating = false;
        }

        Show();
        box.IsCheckedChanged += (_, _) =>
        {
            if (!updating)
            {
                object? next = box.IsChecked;
                if (next is null && !nullable)
                {
                    next = false;
                }

                context.SetValue(next);
            }
        };
        context.Refreshed += (_, _) => Show();
        var scaled = new LayoutTransformControl
        {
            LayoutTransform = new ScaleTransform(0.75, 0.75),
            Child = box,
            Margin = new Thickness(5, 0, 4, 0),
            VerticalAlignment = VerticalAlignment.Center,
        };
        var row = new StackPanel { Orientation = Orientation.Horizontal };
        row.Children.Add(scaled);
        row.Children.Add(label);
        return row;
    }

    /// <summary>
    ///     Creates the editor of an enumeration: a drop-down, or a multi-select drop-down for a <c>[Flags]</c> enumeration.
    /// </summary>
    /// <param name="context">The row context.</param>
    /// <returns>The editor.</returns>
    internal static Control EnumEditor(PropertyEditorContext context)
    {
        var type = Nullable.GetUnderlyingType(context.Item.PropertyType) ?? context.Item.PropertyType;
        if (!type.IsEnum)
        {
            return TextOrChoiceEditor(context);
        }

        return type.IsDefined(typeof(FlagsAttribute), false) ? FlagsEditor(context, type) : ChoiceEditor(context);
    }

    /// <summary>
    ///     Creates a drop-down over the standard values of the item (an exclusive list).
    /// </summary>
    /// <param name="context">The row context.</param>
    /// <returns>The editor.</returns>
    internal static Control ChoiceEditor(PropertyEditorContext context)
    {
        var combo = new ComboBox
        {
            MinHeight = 0,
            Height = EditorHeight,
            Padding = new Thickness(4, 0),
            BorderThickness = new Thickness(0),
            Background = Brushes.Transparent,
            FontSize = GridTheme.FontSize,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalContentAlignment = VerticalAlignment.Center,
            IsEnabled = !context.IsReadOnly,
            ItemTemplate = new global::Avalonia.Controls.Templates.FuncDataTemplate<StandardValue>((value, _) =>
                new TextBlock { Text = value?.Text, FontSize = GridTheme.FontSize }),
        };
        var updating = false;
        void Show()
        {
            updating = true;
            var values = context.Item.GetStandardValues();
            var current = context.Value;
            if (combo.ItemsSource is not IReadOnlyList<StandardValue> shown || !shown.Select(v => v.Text).SequenceEqual(values.Select(v => v.Text)))
            {
                combo.ItemsSource = values;
            }

            combo.SelectedItem = values.FirstOrDefault(v => Equals(v.Value, current));
            updating = false;
        }

        Show();
        combo.SelectionChanged += (_, _) =>
        {
            if (!updating && combo.SelectedItem is StandardValue chosen)
            {
                context.SetValue(chosen.Value);
            }
        };
        context.Refreshed += (_, _) => Show();
        return combo;
    }

    /// <summary>
    ///     Creates an editable drop-down: text that may also be picked from the standard values.
    /// </summary>
    /// <param name="context">The row context.</param>
    /// <returns>The editor.</returns>
    internal static Control EditableChoiceEditor(PropertyEditorContext context)
    {
        var box = new AutoCompleteBox
        {
            MinimumPrefixLength = 0,
            FilterMode = AutoCompleteFilterMode.None,
            MinHeight = 0,
            Height = EditorHeight,
            Padding = new Thickness(4, 0),
            BorderThickness = new Thickness(0),
            Background = Brushes.Transparent,
            FontSize = GridTheme.FontSize,
            IsEnabled = !context.IsReadOnly,
        };

        // The theme's text box inside the template is taller than a grid row; make it as flat as the other text editors
        box.Styles.Add(new global::Avalonia.Styling.Style(x => x.OfType<AutoCompleteBox>().Template().OfType<TextBox>())
        {
            Setters =
            {
                new global::Avalonia.Styling.Setter(TextBox.MinHeightProperty, 0.0),
                new global::Avalonia.Styling.Setter(TextBox.HeightProperty, EditorHeight),
                new global::Avalonia.Styling.Setter(TemplatedControl.PaddingProperty, new Thickness(0)),
                new global::Avalonia.Styling.Setter(TemplatedControl.BorderThicknessProperty, new Thickness(0)),
                new global::Avalonia.Styling.Setter(TextBox.VerticalContentAlignmentProperty, VerticalAlignment.Center),
            },
        });
        var updating = false;
        void Show()
        {
            updating = true;
            box.ItemsSource = context.Item.GetStandardValues().Select(v => v.Text).ToList();
            if (!box.IsKeyboardFocusWithin)
            {
                box.Text = context.PendingText ?? context.Text;
            }

            updating = false;
        }

        Show();
        void Commit()
        {
            var text = box.Text ?? string.Empty;
            if (!updating && (text != context.Text || context.PendingText is not null))
            {
                context.SetText(text);
            }
        }

        box.LostFocus += (_, _) => Commit();
        box.SelectionChanged += (_, _) => Commit();
        box.GotFocus += (_, _) => box.IsDropDownOpen = true;
        context.CommitRequested += (_, _) => Commit();
        context.CancelRequested += (_, _) =>
        {
            context.Cancel();
            box.Text = context.Text;
        };
        context.Refreshed += (_, _) => Show();
        return box;
    }

    /// <summary>
    ///     Creates the multi-select drop-down of a <c>[Flags]</c> enumeration: a button showing the combined text and a
    ///     flyout with one check box per member.
    /// </summary>
    /// <param name="context">The row context.</param>
    /// <param name="enumType">The enumeration type.</param>
    /// <returns>The editor.</returns>
    private static Control FlagsEditor(PropertyEditorContext context, Type enumType)
    {
        var members = Enum.GetValues(enumType).Cast<object>().Where(m => ToBits(m) != 0).Distinct().ToList();
        var checks = new List<(CheckBox Box, ulong Bits)>();
        var panel = new StackPanel { Spacing = 2 };
        var updating = false;
        foreach (var member in members)
        {
            var bits = ToBits(member);
            var check = new CheckBox { Content = member.ToString(), FontSize = GridTheme.FontSize, MinHeight = 0, IsEnabled = !context.IsReadOnly };
            check.IsCheckedChanged += (_, _) =>
            {
                if (updating)
                {
                    return;
                }

                var current = context.Value is { } v ? ToBits(v) : 0;
                var next = check.IsChecked == true ? current | bits : current & ~bits;
                context.SetValue(Enum.ToObject(enumType, next));
            };
            checks.Add((check, bits));
            panel.Children.Add(check);
        }

        var button = new DropDownButton
        {
            Flyout = new Flyout { Content = panel },
            MinHeight = 0,
            Height = EditorHeight,
            Padding = new Thickness(4, 0),
            BorderThickness = new Thickness(0),
            Background = Brushes.Transparent,
            FontSize = GridTheme.FontSize,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Left,
            VerticalContentAlignment = VerticalAlignment.Center,
        };
        void Show()
        {
            updating = true;
            var current = context.Value is { } v ? ToBits(v) : 0;
            foreach (var (box, bits) in checks)
            {
                box.IsChecked = (current & bits) == bits;
            }

            button.Content = new TextBlock { Text = context.Text, TextTrimming = TextTrimming.CharacterEllipsis, FontSize = GridTheme.FontSize };
            updating = false;
        }

        Show();
        context.Refreshed += (_, _) => Show();
        return button;
    }

    /// <summary>
    ///     Gets the bit pattern of an enumeration value.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <returns>The bits as an unsigned number.</returns>
    private static ulong ToBits(object value) =>
        Type.GetTypeCode(Enum.GetUnderlyingType(value.GetType())) is TypeCode.UInt64 or TypeCode.UInt32 or TypeCode.UInt16 or TypeCode.Byte
            ? Convert.ToUInt64(value, CultureInfo.InvariantCulture)
            : unchecked((ulong)Convert.ToInt64(value, CultureInfo.InvariantCulture));

    /// <summary>
    ///     Creates the editor of a collection: a summary text and a button that opens the collection editor.
    /// </summary>
    /// <param name="context">The row context.</param>
    /// <returns>The editor.</returns>
    internal static Control CollectionEditorButton(PropertyEditorContext context)
    {
        var text = NewTextBlock();
        var button = new Button
        {
            Content = "\u2026",
            Width = GridTheme.RowHeight,
            Height = EditorHeight,
            MinHeight = 0,
            MinWidth = 0,
            Padding = new Thickness(0),
            FontSize = GridTheme.FontSize,
            HorizontalContentAlignment = HorizontalAlignment.Center,
            VerticalContentAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Right,
        };
        ToolTip.SetTip(button, "Edit the collection");
        global::Avalonia.Automation.AutomationProperties.SetName(button, "Edit collection");
        void Show()
        {
            var value = context.Value;
            text.Text = value is null ? "(null)" : SummaryText(CollectionTypes.Snapshot(value).Count);
            button.IsEnabled = value is not null;
        }

        Show();
        button.Click += (_, _) => context.Grid.EditCollection(context.Item);
        context.Refreshed += (_, _) => Show();
        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
        grid.Children.Add(text);
        grid.Children.Add(button);
        Grid.SetColumn(button, 1);
        return grid;
    }

    /// <summary>
    ///     Gets the summary text of a collection.
    /// </summary>
    /// <param name="count">The number of elements.</param>
    /// <returns>Text such as <c>(Collection) 3 items</c>.</returns>
    internal static string SummaryText(int count) =>
        string.Create(CultureInfo.CurrentCulture, $"{PropertyItem.CollectionText} {count} {(count == 1 ? "item" : "items")}");

    /// <summary>
    ///     Creates the color editor: a swatch and the hex text (<c>#RRGGBB</c>, or <c>#AARRGGBB</c> when translucent).
    /// </summary>
    /// <param name="context">The row context.</param>
    /// <returns>The editor.</returns>
    internal static Control ColorEditor(PropertyEditorContext context)
    {
        var swatch = new Border
        {
            Width = 14,
            Height = 14,
            CornerRadius = new CornerRadius(2),
            BorderThickness = new Thickness(1),
            Margin = new Thickness(5, 0, 0, 0),
            VerticalAlignment = VerticalAlignment.Center,
        };
        GridTheme.BindBrush(swatch, Border.BorderBrushProperty, GridTheme.SecondaryTextKey);
        Control entry;
        Func<string> read;
        Action<string> write = _ => { };
        if (context.IsReadOnly)
        {
            var block = NewTextBlock();
            entry = block;
            read = () => block.Text ?? string.Empty;
        }
        else
        {
            var box = NewTextBox(string.Empty);
            entry = box;
            read = () => box.Text ?? string.Empty;
            write = text => box.Text = text;
            WireTextCommit(box, context, ShownColorText, text => SetColorText(context, text), false);
        }

        string ShownColorText() => context.PendingText ?? ColorSupport.Format(context.Value);
        void Show()
        {
            var shown = ShownColorText();
            if (context.IsReadOnly)
            {
                ((TextBlock)entry).Text = shown;
            }
            else if (!entry.IsKeyboardFocusWithin && read() != shown)
            {
                write(shown);
            }

            swatch.Background = ColorSupport.TryGetColor(context.Value, out var color) ? new SolidColorBrush(color) : Brushes.Transparent;
        }

        Show();
        context.Refreshed += (_, _) => Show();
        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*") };
        grid.Children.Add(swatch);
        grid.Children.Add(entry);
        Grid.SetColumn(entry, 1);
        return grid;
    }

    /// <summary>
    ///     Interprets color text typed by the user and writes the color.
    /// </summary>
    /// <param name="context">The row context.</param>
    /// <param name="text">The text.</param>
    /// <returns>The outcome.</returns>
    private static PropertyResult SetColorText(PropertyEditorContext context, string text)
    {
        var type = Nullable.GetUnderlyingType(context.Item.PropertyType) ?? context.Item.PropertyType;
        if (string.IsNullOrWhiteSpace(text) && Nullable.GetUnderlyingType(context.Item.PropertyType) is not null)
        {
            return context.SetValue(null);
        }

        return ColorSupport.TryParse(text, type, out var color)
            ? context.SetValue(color)
            : context.RejectText(text, $"'{text}' is not a valid color. Use #RRGGBB, #AARRGGBB or a color name.");
    }

    /// <summary>
    ///     Creates a flat text block that fits a row.
    /// </summary>
    /// <returns>The block.</returns>
    private static TextBlock NewTextBlock()
    {
        var block = new TextBlock
        {
            FontSize = GridTheme.FontSize,
            Padding = new Thickness(5, 0),
            VerticalAlignment = VerticalAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis,
            Opacity = 0.7,
        };
        return block;
    }

    /// <summary>
    ///     Creates a flat, single-line text box that fits a row.
    /// </summary>
    /// <param name="text">The initial text.</param>
    /// <returns>The box.</returns>
    private static TextBox NewTextBox(string text) => new()
    {
        Text = text,
        MinHeight = 0,
        Height = EditorHeight,
        Padding = new Thickness(4, 0),
        BorderThickness = new Thickness(0),
        Background = Brushes.Transparent,
        CornerRadius = new CornerRadius(0),
        FontSize = GridTheme.FontSize,
        VerticalContentAlignment = VerticalAlignment.Center,
        VerticalAlignment = VerticalAlignment.Center,
        AcceptsReturn = false,
    };

    /// <summary>
    ///     Connects a text box to the row: commit on Enter and when focus leaves, cancel on Escape, and show the value
    ///     again when it changes elsewhere.
    /// </summary>
    /// <param name="box">The text box.</param>
    /// <param name="context">The row context.</param>
    /// <param name="shown">Gets the text the box should show.</param>
    /// <param name="commit">Writes the typed text.</param>
    /// <param name="selectAllOnKeyboardFocus">Whether keyboard focus selects the whole text.</param>
    private static void WireTextCommit(TextBox box, PropertyEditorContext context, Func<string> shown, Func<string, PropertyResult> commit, bool selectAllOnKeyboardFocus = true)
    {
        void Commit()
        {
            var text = box.Text ?? string.Empty;
            if (text != shown() || context.PendingText is not null)
            {
                commit(text);
            }
        }

        box.LostFocus += (_, _) => Commit();
        box.GotFocus += (_, e) =>
        {
            if (selectAllOnKeyboardFocus && e.NavigationMethod != NavigationMethod.Pointer)
            {
                box.SelectAll();
            }
        };
        context.CommitRequested += (_, _) => Commit();
        context.CancelRequested += (_, _) =>
        {
            context.Cancel();
            box.Text = shown();
        };
        context.Refreshed += (_, _) =>
        {
            var text = shown();
            if (!box.IsKeyboardFocusWithin && box.Text != text)
            {
                box.Text = text;
            }
        };
    }
}
