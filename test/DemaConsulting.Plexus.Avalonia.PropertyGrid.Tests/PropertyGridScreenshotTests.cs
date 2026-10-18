using Avalonia.Controls;
using Avalonia.Styling;

namespace DemaConsulting.Plexus.Avalonia.PropertyGrid.Tests;

/// <summary>
///     A custom editor used by the tests: a slider for a double property.
/// </summary>
internal static class SliderEditor
{
    /// <summary>
    ///     Creates a registry with the built-in editors plus the <c>slider</c> editor.
    /// </summary>
    /// <returns>The registry.</returns>
    internal static PropertyEditorRegistry<PropertyEditorFactory> CreateRegistry()
    {
        var registry = PropertyGrid.CreateEditorRegistry();
        registry.Register("slider", Create);
        return registry;
    }

    /// <summary>
    ///     Creates the slider editor of a row.
    /// </summary>
    /// <param name="context">The row context.</param>
    /// <returns>The slider.</returns>
    internal static Control Create(PropertyEditorContext context)
    {
        var slider = new Slider
        {
            Minimum = 0,
            Maximum = 100,
            Height = 22,
            MinHeight = 0,
            VerticalAlignment = global::Avalonia.Layout.VerticalAlignment.Center,
            Margin = new global::Avalonia.Thickness(4, 0),
            Value = Convert.ToDouble(context.Value ?? 0.0, System.Globalization.CultureInfo.InvariantCulture),
            IsEnabled = !context.IsReadOnly,
        };
        var updating = false;
        slider.ValueChanged += (_, e) =>
        {
            if (!updating)
            {
                context.SetValue(e.NewValue);
            }
        };
        context.Refreshed += (_, _) =>
        {
            updating = true;
            slider.Value = Convert.ToDouble(context.Value ?? 0.0, System.Globalization.CultureInfo.InvariantCulture);
            updating = false;
        };
        return slider;
    }
}

/// <summary>
///     Captures headless screenshots of the grid and the collection editor so their look can be reviewed.
/// </summary>
public class PropertyGridScreenshotTests
{
    /// <summary>
    ///     The categorized grid in the dark theme, with a nested object expanded, a selection and an invalid staged edit.
    /// </summary>
    /// <returns>A task that completes when the body has run.</returns>
    [Fact]
    public Task PropertyGrid_Screenshot_CategorizedDark() => UiTest.Run(() =>
    {
        var grid = new PropertyGrid { EditorRegistry = SliderEditor.CreateRegistry(), EditMode = PropertyEditMode.Staged, SelectedObject = new Widget { Caption = "Launch", Timeout = 45 } };
        grid.SetExpanded("Size", true);
        grid.SetExpanded("Margins", true);
        var window = GridHarness.Host(grid, 440, 640, ThemeVariant.Dark);
        grid.SelectItem(grid.Session!.FindItem("Mode"));
        grid.Session.SetText(grid.Session.FindItem("Timeout")!, "9999");
        GridHarness.Capture(window, "grid-categorized-dark");
        window.Close();
        Assert.True(File.Exists(Path.Join(Path.GetTempPath(), "plexus-pg", "grid-categorized-dark.png")));
    });

    /// <summary>
    ///     The alphabetical grid in the dark theme.
    /// </summary>
    /// <returns>A task that completes when the body has run.</returns>
    [Fact]
    public Task PropertyGrid_Screenshot_AlphabeticalDark() => UiTest.Run(() =>
    {
        var grid = new PropertyGrid { EditorRegistry = SliderEditor.CreateRegistry(), SortMode = PropertySortMode.Alphabetical, SelectedObject = new Widget() };
        var window = GridHarness.Host(grid, 440, 640, ThemeVariant.Dark);
        GridHarness.Capture(window, "grid-alphabetical-dark");
        window.Close();
        Assert.True(File.Exists(Path.Join(Path.GetTempPath(), "plexus-pg", "grid-alphabetical-dark.png")));
    });

    /// <summary>
    ///     The categorized grid in the light theme.
    /// </summary>
    /// <returns>A task that completes when the body has run.</returns>
    [Fact]
    public Task PropertyGrid_Screenshot_CategorizedLight() => UiTest.Run(() =>
    {
        var grid = new PropertyGrid { EditorRegistry = SliderEditor.CreateRegistry(), SelectedObject = new Widget { Caption = "Launch" } };
        var window = GridHarness.Host(grid, 440, 640, ThemeVariant.Light);
        GridHarness.Capture(window, "grid-categorized-light");
        window.Close();
        Assert.True(File.Exists(Path.Join(Path.GetTempPath(), "plexus-pg", "grid-categorized-light.png")));
    });

    /// <summary>
    ///     The property grid dialog (staged grid with OK and Cancel) in the light theme, showing an invalid value.
    /// </summary>
    /// <returns>A task that completes when the body has run.</returns>
    [Fact]
    public Task PropertyGridDialog_Screenshot_Light() => UiTest.Run(() =>
    {
        var dialog = new PropertyGridDialog(new Widget { Caption = "Launch" });
        dialog.RequestedThemeVariant = ThemeVariant.Light;
        dialog.Show();
        var timeout = GridHarness.Inner<TextBox>(GridHarness.Editor(dialog.Grid, "Timeout"));
        timeout.Focus();
        timeout.Text = "0";
        dialog.Grid.Focus();
        dialog.Accept();
        GridHarness.Capture(dialog, "dialog-light");
        dialog.Close();
        Assert.True(File.Exists(Path.Join(Path.GetTempPath(), "plexus-pg", "dialog-light.png")));
    });

    /// <summary>
    ///     The collection editor dialog in the dark theme.
    /// </summary>
    /// <returns>A task that completes when the body has run.</returns>
    [Fact]
    public Task CollectionEditor_Screenshot_Dark() => UiTest.Run(() =>
    {
        var widget = new Widget();
        var grid = new PropertyGrid { SelectedObject = widget };
        var owner = GridHarness.Host(grid, 440, 640, ThemeVariant.Dark);
        var dialog = new CollectionEditorDialog(grid.Session!, grid.Session!.FindItem("Tags")!);
        dialog.RequestedThemeVariant = ThemeVariant.Dark;
        dialog.Show();
        GridHarness.Pump(dialog);
        GridHarness.Capture(dialog, "collection-editor-dark");
        dialog.Close();
        owner.Close();
        Assert.True(File.Exists(Path.Join(Path.GetTempPath(), "plexus-pg", "collection-editor-dark.png")));
    });
}
