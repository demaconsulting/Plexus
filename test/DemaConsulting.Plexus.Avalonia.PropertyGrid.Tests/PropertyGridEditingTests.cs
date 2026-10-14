using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;

namespace DemaConsulting.Plexus.Avalonia.PropertyGrid.Tests;

/// <summary>
///     Tests of editing through the grid: every editor kind in live and staged mode, validation, and custom editors.
/// </summary>
public class PropertyGridEditingTests
{
    /// <summary>
    ///     Creates a grid over a widget in a window.
    /// </summary>
    /// <param name="widget">The widget.</param>
    /// <param name="mode">The edit mode.</param>
    /// <returns>The grid and its window.</returns>
    private static (PropertyGrid Grid, Window Window) Create(Widget widget, PropertyEditMode mode = PropertyEditMode.Live)
    {
        var grid = new PropertyGrid { EditorRegistry = SliderEditor.CreateRegistry(), EditMode = mode, SelectedObject = widget };
        return (grid, GridHarness.Host(grid));
    }

    /// <summary>
    ///     Types text into a text editor and presses Enter.
    /// </summary>
    /// <param name="window">The window.</param>
    /// <param name="grid">The grid.</param>
    /// <param name="path">The item path.</param>
    /// <param name="text">The text.</param>
    /// <returns>The text box.</returns>
    private static TextBox Type(Window window, PropertyGrid grid, string path, string text)
    {
        var box = GridHarness.Inner<TextBox>(GridHarness.Editor(grid, path));
        box.Focus();
        box.Text = text;
        GridHarness.Press(window, PhysicalKey.Enter);
        return box;
    }

    /// <summary>
    ///     A string is edited in a text box and written when Enter is pressed (live mode).
    /// </summary>
    /// <returns>A task that completes when the body has run.</returns>
    [Fact]
    public Task PropertyEditors_Text_Live_WritesOnEnter() => UiTest.Run(() =>
    {
        var widget = new Widget();
        var (grid, window) = Create(widget);

        var box = Type(window, grid, "Caption", "Launch");

        Assert.Equal("Launch", widget.Caption);
        Assert.Equal(FontWeight.Bold, box.FontWeight);
        Assert.Same(grid, GridHarness.Focused(window));
        window.Close();
    });

    /// <summary>
    ///     Text is also written when the editor loses the focus.
    /// </summary>
    /// <returns>A task that completes when the body has run.</returns>
    [Fact]
    public Task PropertyEditors_Text_Live_WritesWhenFocusLeaves() => UiTest.Run(() =>
    {
        var widget = new Widget();
        var (grid, window) = Create(widget);
        var box = GridHarness.Inner<TextBox>(GridHarness.Editor(grid, "Caption"));
        box.Focus();
        box.Text = "Elsewhere";

        grid.Focus();
        GridHarness.Pump(window);

        Assert.Equal("Elsewhere", widget.Caption);
        window.Close();
    });

    /// <summary>
    ///     In staged mode text is buffered: the object is untouched until the session commits, and cancel restores the value.
    /// </summary>
    /// <returns>A task that completes when the body has run.</returns>
    [Fact]
    public Task PropertyEditors_Text_Staged_BuffersUntilCommit() => UiTest.Run(() =>
    {
        var widget = new Widget();
        var (grid, window) = Create(widget, PropertyEditMode.Staged);

        var box = Type(window, grid, "Caption", "Launch");

        Assert.Equal("Widget", widget.Caption);
        Assert.True(grid.Session!.HasChanges);
        Assert.Equal("Launch", box.Text);
        Assert.Equal(FontWeight.Bold, box.FontWeight);

        grid.Session.Cancel();
        GridHarness.Pump(window);

        Assert.Equal("Widget", widget.Caption);
        Assert.Equal("Widget", box.Text);
        Assert.NotEqual(FontWeight.Bold, box.FontWeight);

        Type(window, grid, "Caption", "Launch");
        Assert.True(grid.Session.Commit().Succeeded);
        Assert.Equal("Launch", widget.Caption);
        window.Close();
    });

    /// <summary>
    ///     The commit scope of the grid wraps the writes of a commit.
    /// </summary>
    /// <returns>A task that completes when the body has run.</returns>
    [Fact]
    public Task PropertyEditors_Staged_CommitRunsInsideTheCommitScope() => UiTest.Run(() =>
    {
        var widget = new Widget();
        var log = new List<string>();
        var (grid, window) = Create(widget, PropertyEditMode.Staged);
        grid.CommitScope = apply =>
        {
            log.Add("begin");
            apply();
            log.Add("end:" + widget.Caption);
        };
        Type(window, grid, "Caption", "Launch");

        grid.Session!.Commit();

        Assert.Equal(["begin", "end:Launch"], log);
        window.Close();
    });

    /// <summary>
    ///     Numeric text that does not convert is rejected: the value is not written, the row shows a red border with the
    ///     reason, and the text stays until it is fixed.
    /// </summary>
    /// <returns>A task that completes when the body has run.</returns>
    [Fact]
    public Task PropertyEditors_InvalidNumber_ShowsErrorUntilFixed() => UiTest.Run(() =>
    {
        var widget = new Widget();
        var (grid, window) = Create(widget);
        var row = GridHarness.Row(grid, "Timeout");

        var box = Type(window, grid, "Timeout", "abc");

        Assert.Equal(30, widget.Timeout);
        Assert.Equal("abc", box.Text);
        Assert.Same(GridTheme.ErrorBrush, row.ValueHost.BorderBrush);
        Assert.False(string.IsNullOrEmpty(ToolTip.GetTip(row.ValueHost) as string));
        Assert.True(grid.Session!.HasErrors);

        Type(window, grid, "Timeout", "60");

        Assert.Equal(60, widget.Timeout);
        Assert.Equal("60", box.Text);
        Assert.NotSame(GridTheme.ErrorBrush, row.ValueHost.BorderBrush);
        Assert.Null(ToolTip.GetTip(row.ValueHost));
        Assert.False(grid.Session.HasErrors);
        window.Close();
    });

    /// <summary>
    ///     A value that violates a validation attribute shows the attribute's message and stays invalid until fixed; in
    ///     staged mode nothing is committed meanwhile.
    /// </summary>
    /// <returns>A task that completes when the body has run.</returns>
    [Fact]
    public Task PropertyEditors_OutOfRangeValue_Staged_ShowsValidationMessage() => UiTest.Run(() =>
    {
        var widget = new Widget();
        var (grid, window) = Create(widget, PropertyEditMode.Staged);
        var row = GridHarness.Row(grid, "Timeout");

        Type(window, grid, "Timeout", "9999");

        Assert.Equal("Timeout must be between 1 and 3600 seconds.", ToolTip.GetTip(row.ValueHost));
        Assert.True(grid.Session!.HasErrors);
        Assert.False(grid.Session.HasChanges);

        Type(window, grid, "Timeout", "120");

        Assert.False(grid.Session.HasErrors);
        Assert.True(grid.Session.HasChanges);
        Assert.Equal(30, widget.Timeout);
        grid.Session.Commit();
        Assert.Equal(120, widget.Timeout);
        window.Close();
    });

    /// <summary>
    ///     Escape on an editor abandons the typed text and the error, and shows the value again.
    /// </summary>
    /// <returns>A task that completes when the body has run.</returns>
    [Fact]
    public Task PropertyEditors_Escape_CancelsTypedText() => UiTest.Run(() =>
    {
        var widget = new Widget();
        var (grid, window) = Create(widget);
        var row = GridHarness.Row(grid, "Timeout");
        var box = GridHarness.Inner<TextBox>(row.Editor);
        box.Focus();
        box.Text = "77";

        GridHarness.Press(window, PhysicalKey.Escape);

        Assert.Equal(30, widget.Timeout);
        Assert.Equal("30", box.Text);
        Assert.Same(grid, GridHarness.Focused(window));

        Type(window, grid, "Timeout", "junk");
        Assert.True(grid.Session!.HasErrors);
        box.Focus();
        GridHarness.Press(window, PhysicalKey.Escape);

        Assert.False(grid.Session.HasErrors);
        Assert.Equal("30", box.Text);
        Assert.NotSame(GridTheme.ErrorBrush, row.ValueHost.BorderBrush);
        window.Close();
    });

    /// <summary>
    ///     Escape while the grid itself has the focus also abandons a rejected edit of the selected row.
    /// </summary>
    /// <returns>A task that completes when the body has run.</returns>
    [Fact]
    public Task PropertyEditors_Escape_OnRowCancelsRejectedEdit() => UiTest.Run(() =>
    {
        var (grid, window) = Create(new Widget());
        var box = Type(window, grid, "Timeout", "junk");
        Assert.True(grid.Session!.HasErrors);

        GridHarness.Press(window, PhysicalKey.Escape);

        Assert.False(grid.Session.HasErrors);
        Assert.Equal("30", box.Text);
        window.Close();
    });

    /// <summary>
    ///     A boolean is edited with a check box.
    /// </summary>
    /// <returns>A task that completes when the body has run.</returns>
    [Fact]
    public Task PropertyEditors_Bool_TogglesWithCheckBox() => UiTest.Run(() =>
    {
        var widget = new Widget();
        var (grid, window) = Create(widget);
        var box = GridHarness.Inner<CheckBox>(GridHarness.Editor(grid, "Visible"));
        Assert.True(box.IsChecked);

        box.IsChecked = false;

        Assert.False(widget.Visible);
        Assert.Equal(FontWeight.Bold, GridHarness.Row(grid, "Visible").Editor.GetValue(global::Avalonia.Controls.Documents.TextElement.FontWeightProperty));
        window.Close();
    });

    /// <summary>
    ///     An enumeration is edited with a drop-down of its members.
    /// </summary>
    /// <returns>A task that completes when the body has run.</returns>
    [Fact]
    public Task PropertyEditors_Enum_SelectsFromDropDown() => UiTest.Run(() =>
    {
        var widget = new Widget();
        var (grid, window) = Create(widget);
        var combo = GridHarness.Inner<ComboBox>(GridHarness.Editor(grid, "Mode"));
        var choices = (IReadOnlyList<StandardValue>)combo.ItemsSource!;
        Assert.Equal(["Normal", "Compact", "Expanded"], choices.Select(c => c.Text));

        combo.SelectedItem = choices[1];

        Assert.Equal(WidgetMode.Compact, widget.Mode);
        window.Close();
    });

    /// <summary>
    ///     A flags enumeration is edited with a multi-select drop-down (a check box per member).
    /// </summary>
    /// <returns>A task that completes when the body has run.</returns>
    [Fact]
    public Task PropertyEditors_FlagsEnum_TogglesMembers() => UiTest.Run(() =>
    {
        var widget = new Widget();
        var (grid, window) = Create(widget);
        var button = GridHarness.Inner<DropDownButton>(GridHarness.Editor(grid, "Features"));
        var boxes = ((StackPanel)((Flyout)button.Flyout!).Content!).Children.Cast<CheckBox>().ToList();
        Assert.Equal(["Animated", "Draggable", "Resizable"], boxes.Select(b => (string)b.Content!));
        Assert.Equal([false, true, false], boxes.Select(b => b.IsChecked == true));

        boxes[0].IsChecked = true;
        boxes[1].IsChecked = false;

        Assert.Equal(WidgetFeatures.Animated, widget.Features);
        Assert.Equal("Animated", ((TextBlock)button.Content!).Text);
        window.Close();
    });

    /// <summary>
    ///     Password text is masked.
    /// </summary>
    /// <returns>A task that completes when the body has run.</returns>
    [Fact]
    public Task PropertyEditors_PasswordText_IsMasked() => UiTest.Run(() =>
    {
        var widget = new Widget();
        var (grid, window) = Create(widget);
        var box = GridHarness.Inner<TextBox>(GridHarness.Editor(grid, "Secret"));

        Assert.Equal('\u25CF', box.PasswordChar);
        Assert.Equal("hunter2", box.Text);

        Type(window, grid, "Secret", "swordfish");

        Assert.Equal("swordfish", widget.Secret);
        window.Close();
    });

    /// <summary>
    ///     A type converter with non-exclusive standard values is edited in an editable drop-down.
    /// </summary>
    /// <returns>A task that completes when the body has run.</returns>
    [Fact]
    public Task PropertyEditors_StandardValues_UseEditableDropDown() => UiTest.Run(() =>
    {
        var person = new Person { Name = "Ada" };
        var grid = new PropertyGrid { SelectedObject = person };
        var window = GridHarness.Host(grid);
        var box = GridHarness.Inner<AutoCompleteBox>(GridHarness.Editor(grid, "Choice"));

        Assert.Equal(["first-Ada", "second"], ((IEnumerable<string>)box.ItemsSource!).ToList());

        var text = GridHarness.Inner<TextBox>(box);
        text.Focus();
        box.Text = "custom";
        GridHarness.Press(window, PhysicalKey.Enter);

        Assert.Equal("custom", person.Choice);
        window.Close();
    });

    /// <summary>
    ///     A color is edited as hex text next to a swatch; an invalid color is rejected with a message.
    /// </summary>
    /// <returns>A task that completes when the body has run.</returns>
    [Fact]
    public Task PropertyEditors_Color_UsesSwatchAndHexText() => UiTest.Run(() =>
    {
        var widget = new Widget();
        var (grid, window) = Create(widget);
        var row = GridHarness.Row(grid, "Fill");
        var box = GridHarness.Inner<TextBox>(row.Editor);
        var swatch = GridHarness.Inner<Border>(row.Editor);
        Assert.Equal("#4682B4", box.Text);
        Assert.Equal(Color.Parse("#4682B4"), ((SolidColorBrush)swatch.Background!).Color);

        Type(window, grid, "Fill", "#FF0000");

        Assert.Equal(Colors.Red, widget.Fill);
        Assert.Equal(Colors.Red, ((SolidColorBrush)swatch.Background).Color);

        Type(window, grid, "Fill", "not a color");

        Assert.Equal(Colors.Red, widget.Fill);
        Assert.Contains("not a valid color", (string)ToolTip.GetTip(row.ValueHost)!, StringComparison.Ordinal);
        window.Close();
    });

    /// <summary>
    ///     Properties of a nested object are edited through its child rows; a struct is written back to its owner.
    /// </summary>
    /// <returns>A task that completes when the body has run.</returns>
    [Fact]
    public Task PropertyEditors_NestedObjects_WriteThroughToOwner() => UiTest.Run(() =>
    {
        var widget = new Widget();
        var (grid, window) = Create(widget);
        grid.SetExpanded("Margins", true);
        grid.SetExpanded("Size", true);
        GridHarness.Pump(window);

        Type(window, grid, "Margins.Left", "9");
        Type(window, grid, "Size.Width", "200");

        Assert.Equal(9, widget.Margins.Left);
        Assert.Equal(200, widget.Size.Width);
        Assert.Equal("9, 4", ((TextBlock)GridHarness.Editor(grid, "Margins")).Text);
        Assert.Equal("200 x 80", ((TextBlock)GridHarness.Editor(grid, "Size")).Text);
        window.Close();
    });

    /// <summary>
    ///     A collection shows a summary and a button that asks for the collection editor; a host handler can take over.
    /// </summary>
    /// <returns>A task that completes when the body has run.</returns>
    [Fact]
    public Task PropertyEditors_Collection_ShowsSummaryAndOpensEditor() => UiTest.Run(() =>
    {
        var widget = new Widget();
        var (grid, window) = Create(widget);
        var requested = new List<string>();
        grid.CollectionEditorRequested += (_, e) =>
        {
            requested.Add(e.Item.Path);
            e.Handled = true;
        };
        var editor = GridHarness.Editor(grid, "Tags");
        Assert.Equal("(Collection) 2 items", GridHarness.Inner<TextBlock>(editor).Text);

        GridHarness.Click(GridHarness.Inner<Button>(editor));

        Assert.Equal(["Tags"], requested);
        widget.Tags.RemoveAt(1);
        grid.Refresh();
        Assert.Equal("(Collection) 1 item", GridHarness.Inner<TextBlock>(GridHarness.Editor(grid, "Tags")).Text);
        window.Close();
    });

    /// <summary>
    ///     A custom editor registered under a key replaces the default editor of properties that request it.
    /// </summary>
    /// <returns>A task that completes when the body has run.</returns>
    [Fact]
    public Task PropertyEditors_CustomEditor_SliderEditsAnnotatedProperty() => UiTest.Run(() =>
    {
        var widget = new Widget();
        var (grid, window) = Create(widget);
        var slider = GridHarness.Inner<Slider>(GridHarness.Editor(grid, "Level"));
        Assert.Equal(25, slider.Value);

        slider.Value = 60;

        Assert.Equal(60, widget.Level);

        widget.Level = 10;
        grid.Refresh();

        Assert.Equal(10, slider.Value);
        window.Close();
    });

    /// <summary>
    ///     Without the registration the annotated property falls back to the editor of its type, and an editor registered
    ///     for a type is used for every property of that type.
    /// </summary>
    /// <returns>A task that completes when the body has run.</returns>
    [Fact]
    public Task PropertyEditors_EditorRegistry_FallsBackAndHonorsTypeRegistrations() => UiTest.Run(() =>
    {
        var registry = PropertyGrid.CreateEditorRegistry();
        var grid = new PropertyGrid { SelectedObject = new Widget() };
        registry.Register(typeof(int), context => new Slider { Minimum = 1, Maximum = 3600, Value = Convert.ToDouble(context.Value, System.Globalization.CultureInfo.InvariantCulture) });
        grid.EditorRegistry = registry;
        var window = GridHarness.Host(grid);

        Assert.IsType<TextBox>(GridHarness.Editor(grid, "Level"));
        Assert.IsType<Slider>(GridHarness.Editor(grid, "Timeout"));
        Assert.Same(PropertyGrid.DefaultEditors, new PropertyGrid().EditorRegistry);
        window.Close();
    });

    /// <summary>
    ///     The row's context menu resets a property to its default value.
    /// </summary>
    /// <returns>A task that completes when the body has run.</returns>
    [Fact]
    public Task PropertyEditors_ResetMenu_RestoresDefaultValue() => UiTest.Run(() =>
    {
        var widget = new Widget { Timeout = 500 };
        var (grid, window) = Create(widget);
        var row = GridHarness.Row(grid, "Timeout");
        var item = (MenuItem)row.ContextMenu!.Items[0]!;

        item.RaiseEvent(new global::Avalonia.Interactivity.RoutedEventArgs(MenuItem.ClickEvent));
        GridHarness.Pump(window);

        Assert.Equal(30, widget.Timeout);
        Assert.Equal("30", GridHarness.Inner<TextBox>(row.Editor).Text);
        window.Close();
    });
}
