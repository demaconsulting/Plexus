namespace DemaConsulting.Plexus.Demo.Model;

/// <summary>
///     One point of a plotted trace, in the units of the plot's axes.
/// </summary>
/// <param name="X">The horizontal value (for example a time in ms or a frequency in Hz).</param>
/// <param name="Y">The vertical value (for example an amplitude or a gain in dB).</param>
public readonly record struct PlotPoint(double X, double Y);

/// <summary>
///     A labelled position on a plot axis, drawn as a grid line with its label.
/// </summary>
/// <param name="Value">The position, in the axis units.</param>
/// <param name="Label">The text shown at the position.</param>
public sealed record PlotTick(double Value, string Label);

/// <summary>
///     One axis of a plot: its range, title and labelled ticks.
/// </summary>
/// <param name="Minimum">The value at the start of the axis.</param>
/// <param name="Maximum">The value at the end of the axis; greater than <paramref name="Minimum"/>.</param>
/// <param name="Title">The axis title, for example <c>Time (ms)</c>.</param>
/// <param name="Ticks">The labelled grid line positions, inside the range.</param>
/// <param name="IsLogarithmic">Whether values are spaced logarithmically (both ends must then be positive).</param>
public sealed record PlotAxis(double Minimum, double Maximum, string Title, IReadOnlyList<PlotTick> Ticks, bool IsLogarithmic = false);

/// <summary>
///     A labelled vertical line on a plot, for example the cutoff frequency of a filter.
/// </summary>
/// <param name="X">The horizontal position, in the X axis units.</param>
/// <param name="Label">The text shown next to the line.</param>
public sealed record PlotMarker(double X, string Label);

/// <summary>
///     A labelled horizontal span of a plot, for example the attack phase of an envelope.
/// </summary>
/// <param name="Start">Where the span starts, in the X axis units.</param>
/// <param name="End">Where the span ends, in the X axis units; not less than <paramref name="Start"/>.</param>
/// <param name="Label">The text shown in the span.</param>
public sealed record PlotRegion(double Start, double End, string Label);

/// <summary>
///     A complete, UI-free description of a small plot: one trace, two axes, optional markers and regions.
/// </summary>
/// <remarks>
///     The demo's inspectors compute these from node settings in the model (see <see cref="AudioPreview"/>) so the math is
///     unit-testable without a UI, and the GUI's plot control only draws them.
/// </remarks>
/// <param name="Points">The trace, in increasing X order.</param>
/// <param name="XAxis">The horizontal axis.</param>
/// <param name="YAxis">The vertical axis.</param>
/// <param name="Markers">The labelled vertical lines.</param>
/// <param name="Regions">The labelled horizontal spans.</param>
public sealed record PlotData(IReadOnlyList<PlotPoint> Points, PlotAxis XAxis, PlotAxis YAxis, IReadOnlyList<PlotMarker> Markers, IReadOnlyList<PlotRegion> Regions);
