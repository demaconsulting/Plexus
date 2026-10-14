using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using DrawingColor = System.Drawing.Color;

namespace DemaConsulting.Plexus.Avalonia.PropertyGrid.Tests;

/// <summary>
///     An object with the color types the grid's color editor supports.
/// </summary>
public sealed class Palette
{
    /// <summary>Gets or sets a System.Drawing color (the type WinForms users know).</summary>
    public DrawingColor Border { get; set; } = DrawingColor.FromArgb(255, 0x12, 0x34, 0x56);

    /// <summary>Gets or sets an optional Avalonia color.</summary>
    public Color? Accent { get; set; } = Colors.Green;
}

/// <summary>
///     Tests of the color editor and of the color text conversion behind it.
/// </summary>
public class ColorEditorTests
{
    /// <summary>
    ///     Colors format as <c>#RRGGBB</c> (or <c>#AARRGGBB</c> when translucent) and parse back into either color type.
    /// </summary>
    [Fact]
    public void ColorSupport_FormatAndParse_RoundTripBothColorTypes()
    {
        Assert.Equal("#123456", ColorSupport.Format(DrawingColor.FromArgb(255, 0x12, 0x34, 0x56)));
        Assert.Equal("#80FF0000", ColorSupport.Format(Color.FromArgb(0x80, 255, 0, 0)));
        Assert.Equal(string.Empty, ColorSupport.Format("not a color"));

        Assert.True(ColorSupport.TryParse(" #FF8800 ", typeof(DrawingColor), out var drawing));
        Assert.Equal(DrawingColor.FromArgb(255, 255, 0x88, 0), drawing);
        Assert.True(ColorSupport.TryParse("red", typeof(Color), out var avalonia));
        Assert.Equal(Colors.Red, avalonia);
        Assert.False(ColorSupport.TryParse("nonsense", typeof(Color), out _));
        Assert.False(ColorSupport.TryGetColor(null, out _));
    }

    /// <summary>
    ///     A System.Drawing color and an optional Avalonia color are edited as hex text; empty text clears the optional one.
    /// </summary>
    /// <returns>A task that completes when the body has run.</returns>
    [Fact]
    public Task ColorEditor_EditsBothColorTypes() => UiTest.Run(() =>
    {
        var palette = new Palette();
        var grid = new PropertyGrid { SelectedObject = palette };
        var window = GridHarness.Host(grid);
        var border = GridHarness.Inner<TextBox>(GridHarness.Editor(grid, "Border"));
        var accent = GridHarness.Inner<TextBox>(GridHarness.Editor(grid, "Accent"));
        Assert.Equal("#123456", border.Text);
        Assert.Equal("#008000", accent.Text);

        border.Focus();
        border.Text = "#FF0000";
        GridHarness.Press(window, PhysicalKey.Enter);
        accent.Focus();
        accent.Text = string.Empty;
        GridHarness.Press(window, PhysicalKey.Enter);

        Assert.Equal(DrawingColor.FromArgb(255, 255, 0, 0), palette.Border);
        Assert.Null(palette.Accent);
        window.Close();
    });
}
