using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using DemaConsulting.Plexus.Demo.Model;

namespace DemaConsulting.Plexus.Demo.Gui.Inspectors;

/// <summary>
///     A small, theme-aware plot of one trace: grid lines, axes with tick labels and titles, labelled regions and markers,
///     drawn directly with a <see cref="DrawingContext"/>.
/// </summary>
/// <remarks>
///     <para>
///         The demo's audio inspectors use it for the oscillator waveform, the ADSR envelope and the filter response, and
///         their tooltips use it small (<see cref="IsCompact"/>). It is deliberately tiny and has no charting
///         dependency: a host migrating a custom inspector needs only <c>Render</c>. What to draw comes from
///         <see cref="PlotData"/>, computed in the model (<see cref="AudioPreview"/>) so the math stays unit-testable.
///     </para>
///     <para>
///         The colors come from the Fluent theme resources of the control's actual theme variant (text, lines, background
///         and the accent for the trace), so the plot follows light and dark mode and redraws when the variant changes.
///         Use it from the UI thread.
///     </para>
/// </remarks>
internal sealed class SignalPlot : Control
{
    /// <summary>
    ///     Identifies the <see cref="Data"/> property.
    /// </summary>
    public static readonly StyledProperty<PlotData?> DataProperty = AvaloniaProperty.Register<SignalPlot, PlotData?>(nameof(Data));

    /// <summary>
    ///     Identifies the <see cref="IsCompact"/> property.
    /// </summary>
    public static readonly StyledProperty<bool> IsCompactProperty = AvaloniaProperty.Register<SignalPlot, bool>(nameof(IsCompact));

    /// <summary>
    ///     The theme resource of the labels and titles.
    /// </summary>
    internal const string TextKey = "SystemControlForegroundBaseHighBrush";

    /// <summary>
    ///     The theme resource of the axes.
    /// </summary>
    internal const string AxisKey = "SystemControlForegroundBaseMediumBrush";

    /// <summary>
    ///     The theme resource of the grid lines and region bands.
    /// </summary>
    internal const string GridKey = "SystemControlForegroundBaseLowBrush";

    /// <summary>
    ///     The theme resource of the trace and markers.
    /// </summary>
    internal const string TraceKey = "SystemControlHighlightAccentBrush";

    /// <summary>
    ///     The theme resource of the plot background.
    /// </summary>
    internal const string BackgroundKey = "SystemControlBackgroundAltHighBrush";

    /// <summary>
    ///     The space left of the plot area for the amplitude labels, in device-independent pixels.
    /// </summary>
    private const double LeftMargin = 44;

    /// <summary>
    ///     The space right of the plot area, so the last time label is not cut off.
    /// </summary>
    private const double RightMargin = 14;

    /// <summary>
    ///     The space above the plot area for the vertical axis title.
    /// </summary>
    private const double TopMargin = 20;

    /// <summary>
    ///     The space below the plot area for the tick labels and the horizontal axis title.
    /// </summary>
    private const double BottomMargin = 36;

    /// <summary>
    ///     The inner padding of a compact plot.
    /// </summary>
    private const double CompactPadding = 3;

    /// <summary>
    ///     The font size of labels and titles.
    /// </summary>
    private const double FontSize = 10;

    /// <summary>
    ///     The least distance between a region label and the left or right edge of the plot area.
    /// </summary>
    private const double LabelPadding = 3;

    /// <summary>
    ///     The least horizontal gap between two region labels.
    /// </summary>
    private const double LabelGap = 4;

    /// <summary>
    ///     The size a full plot asks for when its container leaves it unbounded.
    /// </summary>
    private static readonly Size FullSize = new(360, 240);

    /// <summary>
    ///     The size a compact plot asks for when its container leaves it unbounded.
    /// </summary>
    private static readonly Size CompactSize = new(160, 60);

    /// <summary>
    ///     The fallback brushes, used when the theme does not define a resource (for example without the Fluent theme).
    /// </summary>
    private static readonly Dictionary<string, IBrush> Fallbacks = new(StringComparer.Ordinal)
    {
        [TextKey] = Brushes.Black,
        [AxisKey] = Brushes.DimGray,
        [GridKey] = new ImmutableSolidColorBrush(Color.FromArgb(0x40, 0x80, 0x80, 0x80)),
        [TraceKey] = new ImmutableSolidColorBrush(Color.Parse("#0078D7")),
        [BackgroundKey] = Brushes.Transparent,
    };

    /// <summary>
    ///     Initializes static members of the <see cref="SignalPlot"/> class: new data or a new mode redraws the plot.
    /// </summary>
    static SignalPlot()
    {
        AffectsRender<SignalPlot>(DataProperty, IsCompactProperty);
        AffectsMeasure<SignalPlot>(IsCompactProperty);
    }

    /// <summary>
    ///     Initializes a new instance of the <see cref="SignalPlot"/> class.
    /// </summary>
    public SignalPlot()
    {
        // The brushes are looked up while rendering, so a change of light or dark mode only needs a redraw
        ActualThemeVariantChanged += (_, _) => InvalidateVisual();
    }

    /// <summary>
    ///     Gets or sets what to draw; <see langword="null"/> draws only the background.
    /// </summary>
    public PlotData? Data
    {
        get => GetValue(DataProperty);
        set => SetValue(DataProperty, value);
    }

    /// <summary>
    ///     Gets or sets a value indicating whether the plot is a thumbnail: only the trace and the zero line, no labels,
    ///     axes or grid lines.
    /// </summary>
    public bool IsCompact
    {
        get => GetValue(IsCompactProperty);
        set => SetValue(IsCompactProperty, value);
    }

    /// <summary>
    ///     Gets the brush the plot uses for a theme resource in its current theme variant.
    /// </summary>
    /// <param name="key">One of the resource keys of this class, for example <see cref="TextKey"/>.</param>
    /// <returns>The theme's brush, or the fallback when the theme has none.</returns>
    internal IBrush GetBrush(string key) =>
        this.TryFindResource(key, ActualThemeVariant, out var value) && value is IBrush brush
            ? brush
            : Fallbacks.GetValueOrDefault(key, Brushes.Gray);

    /// <inheritdoc />
    public override void Render(DrawingContext context)
    {
        // The background shows even without data, so an empty plot still reads as a plot
        var bounds = new Rect(Bounds.Size);
        if (bounds.Width <= 0 || bounds.Height <= 0)
        {
            return;
        }

        context.DrawRectangle(GetBrush(BackgroundKey), null, bounds);
        if (Data is not { } data)
        {
            return;
        }

        // The plot area: inside the labels for a full plot, almost the whole control for a thumbnail
        var area = IsCompact
            ? bounds.Deflate(CompactPadding)
            : new Rect(LeftMargin, TopMargin, bounds.Width - LeftMargin - RightMargin, bounds.Height - TopMargin - BottomMargin);
        if (area.Width <= 1 || area.Height <= 1)
        {
            return;
        }

        var map = new PlotMap(data, area);
        if (IsCompact)
        {
            DrawZeroLine(context, data, map, area);
        }
        else
        {
            DrawRegions(context, data, map, area);
            DrawGrid(context, data, map, area);
            DrawAxes(context, data, area, bounds);
            DrawMarkers(context, data, map, area);
        }

        // The trace is clipped to the plot area, so values beyond an axis (a deep filter notch) do not spill over the labels
        using (context.PushClip(area))
        {
            DrawTrace(context, data, map);
        }
    }

    /// <inheritdoc />
    protected override Size MeasureOverride(Size availableSize)
    {
        // Fill a bounded container; ask for a sensible size in an unbounded one
        var preferred = IsCompact ? CompactSize : FullSize;
        return new Size(
            double.IsFinite(availableSize.Width) ? availableSize.Width : preferred.Width,
            double.IsFinite(availableSize.Height) ? availableSize.Height : preferred.Height);
    }

    /// <summary>
    ///     Formats a label in the plot's font.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <param name="brush">The color.</param>
    /// <returns>The formatted text.</returns>
    private static FormattedText Text(string text, IBrush brush) =>
        new(text, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, Typeface.Default, FontSize, brush);

    /// <summary>
    ///     Draws the labelled regions as alternating bands behind everything else.
    /// </summary>
    /// <param name="context">The drawing context.</param>
    /// <param name="data">The plot.</param>
    /// <param name="map">The value-to-pixel mapping.</param>
    /// <param name="area">The plot area.</param>
    private void DrawRegions(DrawingContext context, PlotData data, PlotMap map, Rect area)
    {
        var band = GetBrush(GridKey);
        var text = GetBrush(TextKey);
        var labels = new List<FormattedText>();
        var spans = new List<(double Left, double Right, double Width)>();
        for (var i = 0; i < data.Regions.Count; i++)
        {
            var region = data.Regions[i];
            var left = map.X(region.Start);
            var right = map.X(region.End);

            // Every other band is shaded so neighboring phases stay distinguishable
            if (i % 2 == 0 && right > left)
            {
                using (context.PushOpacity(0.5))
                {
                    context.DrawRectangle(band, null, new Rect(left, area.Top, right - left, area.Height));
                }
            }

            var label = Text(region.Label, text);
            labels.Add(label);
            spans.Add((left, right, label.Width));
        }

        // The labels go inside the area, clear of the tick labels and of each other; one with no room is left out
        var positions = LayoutRegionLabels(spans, area.Left, area.Right);
        for (var i = 0; i < labels.Count; i++)
        {
            if (positions[i] is { } x)
            {
                context.DrawText(labels[i], new Point(x, area.Top + 2));
            }
        }
    }

    /// <summary>
    ///     Places the labels of the regions along the top of the plot area.
    /// </summary>
    /// <remarks>
    ///     A label is centered over its region when that keeps it inside the area and clear of the label before it.
    ///     Otherwise it is shifted: right to stay at least <see cref="LabelPadding"/> inside the left edge of the area (so
    ///     it never reaches the tick labels, which end left of the area) and right of the previous label by at least
    ///     <see cref="LabelGap"/>; left to stay inside the right edge. A label that cannot be placed that way, or that would
    ///     end up more than <see cref="LabelGap"/> away from its own region (so it would name the wrong phase), is skipped.
    ///     Pure, so the rules are unit-tested without drawing.
    /// </remarks>
    /// <param name="labels">The regions in drawing order: their left and right pixel columns and their label widths.</param>
    /// <param name="areaLeft">The left edge of the plot area.</param>
    /// <param name="areaRight">The right edge of the plot area.</param>
    /// <returns>The left edge of each label, or <see langword="null"/> for a label that is skipped.</returns>
    internal static IReadOnlyList<double?> LayoutRegionLabels(IReadOnlyList<(double Left, double Right, double Width)> labels, double areaLeft, double areaRight)
    {
        var positions = new List<double?>(labels.Count);
        var minimum = areaLeft + LabelPadding;
        var maximum = areaRight - LabelPadding;
        var previousRight = double.NegativeInfinity;
        foreach (var (left, right, width) in labels)
        {
            // Start centered, then push right past the area edge and the previous label, then pull back from the right edge
            var x = ((left + right) / 2) - (width / 2);
            x = Math.Max(x, Math.Max(minimum, previousRight + LabelGap));
            x = Math.Min(x, maximum - width);

            // Pulling back may make it collide again, and a label far from its region would be misleading: skip it then
            var fits = x >= minimum && x >= previousRight + LabelGap;
            var nearItsRegion = x <= right + LabelGap && x + width >= left - LabelGap;
            if (fits && nearItsRegion)
            {
                positions.Add(x);
                previousRight = x + width;
            }
            else
            {
                positions.Add(null);
            }
        }

        return positions;
    }

    /// <summary>
    ///     Computes where a vertical-axis tick label is drawn: right-aligned left of the plot area, centered on its grid line.
    /// </summary>
    /// <param name="area">The plot area.</param>
    /// <param name="y">The pixel row of the tick.</param>
    /// <param name="label">The size of the label.</param>
    /// <returns>The label's bounds.</returns>
    internal static Rect YTickLabelBounds(Rect area, double y, Size label) =>
        new(area.Left - 4 - label.Width, y - (label.Height / 2), label.Width, label.Height);

    /// <summary>
    ///     Computes where the vertical axis title is drawn: above the plot area, left-aligned with it.
    /// </summary>
    /// <remarks>
    ///     The tick labels end <c>4</c> pixels left of the area and the top one is centered on the area's top edge, so a title
    ///     that starts at the area's left edge never overlaps them, whatever their width; it is kept at least at the top of the
    ///     control.
    /// </remarks>
    /// <param name="area">The plot area.</param>
    /// <param name="title">The size of the title.</param>
    /// <returns>The top-left corner of the title.</returns>
    internal static Point YAxisTitleOrigin(Rect area, Size title) =>
        new(area.Left, Math.Max(0, area.Top - title.Height - 4));

    /// <summary>
    ///     Draws the grid lines at the ticks of both axes, with the tick labels outside the plot area.
    /// </summary>
    /// <param name="context">The drawing context.</param>
    /// <param name="data">The plot.</param>
    /// <param name="map">The value-to-pixel mapping.</param>
    /// <param name="area">The plot area.</param>
    private void DrawGrid(DrawingContext context, PlotData data, PlotMap map, Rect area)
    {
        var grid = new Pen(GetBrush(GridKey), 1);
        var text = GetBrush(TextKey);

        // Horizontal grid lines with the amplitude labels right-aligned to the left of the area
        foreach (var tick in data.YAxis.Ticks)
        {
            var y = map.Y(tick.Value);
            context.DrawLine(grid, new Point(area.Left, y), new Point(area.Right, y));
            var label = Text(tick.Label, text);
            context.DrawText(label, YTickLabelBounds(area, y, new Size(label.Width, label.Height)).TopLeft);
        }

        // Vertical grid lines with the time or frequency labels centered below the area
        foreach (var tick in data.XAxis.Ticks)
        {
            var x = map.X(tick.Value);
            context.DrawLine(grid, new Point(x, area.Top), new Point(x, area.Bottom));
            var label = Text(tick.Label, text);
            context.DrawText(label, new Point(x - (label.Width / 2), area.Bottom + 3));
        }
    }

    /// <summary>
    ///     Draws the two axis lines and their titles.
    /// </summary>
    /// <param name="context">The drawing context.</param>
    /// <param name="data">The plot.</param>
    /// <param name="area">The plot area.</param>
    /// <param name="bounds">The whole control.</param>
    private void DrawAxes(DrawingContext context, PlotData data, Rect area, Rect bounds)
    {
        var axis = new Pen(GetBrush(AxisKey), 1);
        context.DrawLine(axis, area.TopLeft, area.BottomLeft);
        context.DrawLine(axis, area.BottomLeft, area.BottomRight);

        // The horizontal title is centered under the tick labels; the vertical title sits above the area, starting at its
        // left edge so it stays clear of the top tick label
        var text = GetBrush(TextKey);
        var xTitle = Text(data.XAxis.Title, text);
        context.DrawText(xTitle, new Point(area.Left + ((area.Width - xTitle.Width) / 2), bounds.Bottom - xTitle.Height - 2));
        var yTitle = Text(data.YAxis.Title, text);
        context.DrawText(yTitle, YAxisTitleOrigin(area, new Size(yTitle.Width, yTitle.Height)));
    }

    /// <summary>
    ///     Draws the markers as dashed vertical lines with their labels.
    /// </summary>
    /// <param name="context">The drawing context.</param>
    /// <param name="data">The plot.</param>
    /// <param name="map">The value-to-pixel mapping.</param>
    /// <param name="area">The plot area.</param>
    private void DrawMarkers(DrawingContext context, PlotData data, PlotMap map, Rect area)
    {
        var brush = GetBrush(TraceKey);
        var pen = new Pen(brush, 1, new DashStyle([4, 3], 0));
        foreach (var marker in data.Markers)
        {
            var x = map.X(marker.X);
            context.DrawLine(pen, new Point(x, area.Top), new Point(x, area.Bottom));
            var label = Text(marker.Label, brush);

            // Keep the label inside the area: right of the line, or left of it near the right edge
            var left = x + 3 + label.Width > area.Right ? x - 3 - label.Width : x + 3;
            context.DrawText(label, new Point(left, area.Top + 2));
        }
    }

    /// <summary>
    ///     Draws the zero line of a thumbnail, when zero is on the vertical axis.
    /// </summary>
    /// <param name="context">The drawing context.</param>
    /// <param name="data">The plot.</param>
    /// <param name="map">The value-to-pixel mapping.</param>
    /// <param name="area">The plot area.</param>
    private void DrawZeroLine(DrawingContext context, PlotData data, PlotMap map, Rect area)
    {
        if (data.YAxis.Minimum <= 0 && data.YAxis.Maximum >= 0)
        {
            var y = map.Y(0);
            context.DrawLine(new Pen(GetBrush(GridKey), 1), new Point(area.Left, y), new Point(area.Right, y));
        }
    }

    /// <summary>
    ///     Draws the trace as one polyline in the accent color.
    /// </summary>
    /// <param name="context">The drawing context.</param>
    /// <param name="data">The plot.</param>
    /// <param name="map">The value-to-pixel mapping.</param>
    private void DrawTrace(DrawingContext context, PlotData data, PlotMap map)
    {
        if (data.Points.Count < 2)
        {
            return;
        }

        var geometry = new StreamGeometry();
        using (var figure = geometry.Open())
        {
            figure.BeginFigure(map.Point(data.Points[0]), false);
            for (var i = 1; i < data.Points.Count; i++)
            {
                figure.LineTo(map.Point(data.Points[i]));
            }

            figure.EndFigure(false);
        }

        var pen = new Pen(GetBrush(TraceKey), IsCompact ? 1.5 : 2, lineJoin: PenLineJoin.Round);
        context.DrawGeometry(null, pen, geometry);
    }

    /// <summary>
    ///     Maps plot values to pixels in the plot area, linearly or logarithmically per axis.
    /// </summary>
    /// <param name="data">The plot whose axes define the mapping.</param>
    /// <param name="area">The plot area.</param>
    private readonly struct PlotMap(PlotData data, Rect area)
    {
        /// <summary>
        ///     Maps a point.
        /// </summary>
        /// <param name="point">The point in axis units.</param>
        /// <returns>The pixel position.</returns>
        public Point Point(PlotPoint point) => new(X(point.X), Y(point.Y));

        /// <summary>
        ///     Maps a horizontal value.
        /// </summary>
        /// <param name="value">The value in X axis units.</param>
        /// <returns>The pixel column.</returns>
        public double X(double value) => area.Left + (Fraction(data.XAxis, value) * area.Width);

        /// <summary>
        ///     Maps a vertical value; larger values are higher up.
        /// </summary>
        /// <param name="value">The value in Y axis units.</param>
        /// <returns>The pixel row.</returns>
        public double Y(double value) => area.Bottom - (Fraction(data.YAxis, value) * area.Height);

        /// <summary>
        ///     Gets where a value lies along an axis, 0 at the minimum and 1 at the maximum (outside for values beyond it).
        /// </summary>
        /// <param name="axis">The axis.</param>
        /// <param name="value">The value.</param>
        /// <returns>The fraction; 0 for a degenerate axis.</returns>
        private static double Fraction(PlotAxis axis, double value)
        {
            if (axis.IsLogarithmic)
            {
                var low = Math.Log10(axis.Minimum);
                var span = Math.Log10(axis.Maximum) - low;
                return span > 0 && value > 0 ? (Math.Log10(value) - low) / span : 0;
            }

            var range = axis.Maximum - axis.Minimum;
            return range > 0 ? (value - axis.Minimum) / range : 0;
        }
    }
}
