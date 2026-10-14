using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using Avalonia.Media;

namespace DemaConsulting.Plexus.Avalonia.PropertyGrid.Tests;

/// <summary>
///     A display mode used as an exclusive enumeration.
/// </summary>
public enum WidgetMode
{
    /// <summary>The normal mode.</summary>
    Normal,

    /// <summary>The compact mode.</summary>
    Compact,

    /// <summary>The expanded mode.</summary>
    Expanded,
}

/// <summary>
///     Optional features used as a flags enumeration.
/// </summary>
[Flags]
public enum WidgetFeatures
{
    /// <summary>No features.</summary>
    None = 0,

    /// <summary>The widget animates.</summary>
    Animated = 1,

    /// <summary>The widget can be dragged.</summary>
    Draggable = 2,

    /// <summary>The widget can be resized.</summary>
    Resizable = 4,
}

/// <summary>
///     Margins around a widget (a nested, expandable reference object).
/// </summary>
[TypeConverter(typeof(ExpandableObjectConverter))]
public sealed class Margins
{
    /// <summary>Gets or sets the left margin.</summary>
    [DefaultValue(0)]
    public int Left { get; set; }

    /// <summary>Gets or sets the right margin.</summary>
    [DefaultValue(0)]
    public int Right { get; set; }

    /// <inheritdoc />
    public override string ToString() => $"{Left}, {Right}";
}

/// <summary>
///     A size (an expandable value type with a readable text).
/// </summary>
[TypeConverter(typeof(ExpandableObjectConverter))]
public struct Extent
{
    /// <summary>Gets or sets the width.</summary>
    public int Width { get; set; }

    /// <summary>Gets or sets the height.</summary>
    public int Height { get; set; }

    /// <inheritdoc />
    public override readonly string ToString() => $"{Width} x {Height}";
}

/// <summary>
///     A neutral sample object that exercises every editor kind of the grid.
/// </summary>
public sealed class Widget
{
    /// <summary>Gets or sets the caption.</summary>
    [Category("Appearance")]
    [Description("The caption shown on the widget.")]
    [DefaultValue("Widget")]
    public string Caption { get; set; } = "Widget";

    /// <summary>Gets or sets the fill color.</summary>
    [Category("Appearance")]
    [Description("The color that fills the widget.")]
    public Color Fill { get; set; } = Color.Parse("#4682B4");

    /// <summary>Gets or sets the opacity.</summary>
    [Category("Appearance")]
    [Description("How opaque the widget is, from 0 to 1.")]
    [DefaultValue(1.0)]
    [Range(0.0, 1.0, ErrorMessage = "Opacity must be between 0 and 1.")]
    public double Opacity { get; set; } = 1.0;

    /// <summary>Gets or sets a value indicating whether the widget is visible.</summary>
    [Category("Appearance")]
    [Description("Whether the widget is drawn.")]
    [DefaultValue(true)]
    public bool Visible { get; set; } = true;

    /// <summary>Gets or sets the mode.</summary>
    [Category("Behavior")]
    [Description("The display mode.")]
    [DefaultValue(WidgetMode.Normal)]
    public WidgetMode Mode { get; set; }

    /// <summary>Gets or sets the features.</summary>
    [Category("Behavior")]
    [Description("The optional features that are switched on.")]
    public WidgetFeatures Features { get; set; } = WidgetFeatures.Draggable;

    /// <summary>Gets or sets the timeout.</summary>
    [Category("Behavior")]
    [Description("Seconds before the widget goes idle.")]
    [DefaultValue(30)]
    [Range(1, 3600, ErrorMessage = "Timeout must be between 1 and 3600 seconds.")]
    public int Timeout { get; set; } = 30;

    /// <summary>Gets or sets a secret.</summary>
    [Category("Behavior")]
    [PasswordPropertyText(true)]
    public string Secret { get; set; } = "hunter2";

    /// <summary>Gets or sets the level (edited by a slider when one is registered).</summary>
    [Category("Behavior")]
    [Description("The output level.")]
    [PropertyEditor("slider")]
    public double Level { get; set; } = 25;

    /// <summary>Gets or sets the size.</summary>
    [Category("Layout")]
    [Description("The size of the widget.")]
    public Extent Size { get; set; } = new() { Width = 120, Height = 80 };

    /// <summary>Gets or sets the margins.</summary>
    [Category("Layout")]
    [Description("The space around the widget.")]
    [TypeConverter(typeof(ExpandableObjectConverter))]
    public Margins Margins { get; set; } = new() { Left = 4, Right = 4 };

    /// <summary>Gets or sets the tags.</summary>
    [Category("Data")]
    [Description("Labels attached to the widget.")]
    public List<Tag> Tags { get; set; } = [new Tag { Label = "primary" }, new Tag { Label = "pinned" }];

    /// <summary>Gets or sets the scores.</summary>
    [Category("Data")]
    public List<string> Notes { get; set; } = ["first", "second"];

    /// <summary>Gets the identifier.</summary>
    [Category("Data")]
    [Description("The identifier assigned by the system.")]
    [ReadOnly(true)]
    public string Id { get; set; } = "W-1042";

    /// <summary>Gets or sets a hidden value.</summary>
    [Browsable(false)]
    public string Hidden { get; set; } = "hidden";
}
