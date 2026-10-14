namespace DemaConsulting.Plexus.Scene;

/// <summary>
///     The appearance of one wire: any member left null falls back to the <see cref="DiagramTheme"/>.
/// </summary>
/// <remarks>
///     <para>
///         Hosts return a style from <see cref="Diagram.WireStyler"/> to color, thicken, dash or
///         remove the arrowhead of individual wires. Because the styler is evaluated for every wire
///         each time a scene is built, the style follows whatever the wire is connected to.
///     </para>
///     <para>
///         A style is immutable and validated when created: <see cref="Thickness"/> must be greater
///         than zero, and every <see cref="Dash"/> entry must be a finite number greater than zero.
///         A dash pattern alternates "on" and "off" lengths in diagram units; like SVG, a pattern with
///         an odd number of entries is repeated once to make it even, so <c>[6]</c> means 6 on, 6 off.
///         The dash pattern starts at the source end of the wire.
///     </para>
///     <para>
///         Derive variants with <c>style with { Thickness = 4 }</c>; a null member means "use the theme". Combine two styles with <see cref="WithOverrides"/>.
///     </para>
/// </remarks>
/// <example>
///     Styles for individual wires.
///     <code>
///     using DemaConsulting.Plexus.Geometry;
///     using DemaConsulting.Plexus.Scene;
///
///     var hot = new WireStyle { Color = Color.Red, Thickness = 4 };            // everything else comes from the theme
///     var dashed = new WireStyle { Dash = [6, 4], Arrowhead = false };         // 6 on, 4 off, no arrowhead
///     var thickHot = hot with { Thickness = 6 };                                // derive a variant (a record)
///     var both = hot.WithOverrides(dashed);                                     // red, 4 thick, dashed, no arrowhead
///     </code>
/// </example>
public sealed record WireStyle
{
    /// <summary>
    ///     The thickness, or null to use the theme.
    /// </summary>
    private readonly double? _thickness;

    /// <summary>
    ///     The dash pattern, or null for a solid line.
    /// </summary>
    private readonly double[]? _dash;

    /// <summary>
    ///     Initializes a new instance of the <see cref="WireStyle"/> class as a style that sets nothing, so every member comes from the theme.
    /// </summary>
    public WireStyle()
    {
        // Nothing to initialize: every member has a usable default
    }

    /// <summary>
    ///     Gets or initializes the wire (and arrowhead) color; null (the default) uses <see cref="DiagramTheme.WireColor"/>.
    /// </summary>
    public Color? Color { get; init; }

    /// <summary>
    ///     Gets or initializes the line thickness; null (the default) uses <see cref="DiagramTheme.WireThickness"/>.
    /// </summary>
    /// <remarks>
    ///     The arrowhead grows at half the rate of the thickness so heavy wires keep a proportionate head.
    /// </remarks>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when set to zero, a negative number or a non-finite number.</exception>
    public double? Thickness
    {
        get => _thickness;
        init
        {
            if (value is { } thickness && (!double.IsFinite(thickness) || thickness <= 0))
            {
                throw new ArgumentOutOfRangeException(nameof(value), value, "The wire thickness must be greater than zero.");
            }

            _thickness = value;
        }
    }

    /// <summary>
    ///     Gets or initializes the dash pattern (alternating on and off lengths in diagram units); null or empty (the default) means a solid line.
    /// </summary>
    /// <remarks>
    ///     The list is copied when set, so later changes to the caller's list do not affect the style.
    /// </remarks>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when an entry is zero, negative or not finite.</exception>
    public IReadOnlyList<double>? Dash
    {
        get => _dash;
        init
        {
            if (value is null)
            {
                _dash = null;
                return;
            }

            var invalid = value.Where(l => !double.IsFinite(l) || l <= 0).Cast<double?>().FirstOrDefault();
            if (invalid is { } length)
            {
                throw new ArgumentOutOfRangeException(nameof(value), length, "Every dash length must be greater than zero.");
            }

            _dash = [.. value];
        }
    }

    /// <summary>
    ///     Gets or initializes whether the wire ends in an arrowhead; null (the default) uses <see cref="DiagramTheme.WireArrowheads"/>.
    /// </summary>
    public bool? Arrowhead { get; init; }

    /// <summary>
    ///     Gets a style whose set members come from <paramref name="overrides"/> and whose other members come from this style.
    /// </summary>
    /// <param name="overrides">The style that wins where it has a value; null leaves this style unchanged.</param>
    /// <returns>The combined style.</returns>
    public WireStyle WithOverrides(WireStyle? overrides) =>
        overrides is null
            ? this
            : new WireStyle
            {
                Color = overrides.Color ?? Color,
                Thickness = overrides.Thickness ?? Thickness,
                Dash = overrides.Dash ?? Dash,
                Arrowhead = overrides.Arrowhead ?? Arrowhead,
            };

    /// <summary>
    ///     Gets the dash pattern with an odd number of entries repeated once, or an empty array for a solid line.
    /// </summary>
    /// <returns>The even-length pattern of alternating on and off lengths.</returns>
    internal double[] GetEffectiveDash()
    {
        if (_dash is null or [])
        {
            return [];
        }

        return _dash.Length % 2 == 0 ? _dash : [.. _dash, .. _dash];
    }
}
