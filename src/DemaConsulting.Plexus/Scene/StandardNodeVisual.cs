namespace DemaConsulting.Plexus.Scene;

/// <summary>
///     The standard node template: a dark box with a title, icon, name, summary and typed port triangles.
/// </summary>
/// <remarks>
///     This is what nodes look like unless <see cref="NodeType.Visual"/> says otherwise. It is
///     public and not sealed so custom visuals can wrap it (call <see cref="Build"/> and then add
///     decoration) or override individual parts. <see cref="Build"/> is virtual and calls the protected
///     virtual members in this order, bottom to top: <c>AddGlow</c>, <c>AddBox</c>, <c>AddTitle</c>, <c>AddIcon</c>,
///     <c>AddName</c>, <c>AddSummary</c>, <c>AddPorts</c>. Override one of them (calling <c>base</c> to keep the
///     standard drawing, or not to replace it) or override <see cref="Build"/> itself. Stateless and thread-safe.
/// </remarks>
/// <example>
///     Override one part of the standard template: here an accent bar along the top of every node of a type.
///     <code>
///     using DemaConsulting.Plexus;
///     using DemaConsulting.Plexus.Geometry;
///     using DemaConsulting.Plexus.Scene;
///
///     var type = new NodeType("demo.Accent") { Visual = new AccentBarVisual() };   // StandardNodeVisual.Instance is the default
///
///     public sealed class AccentBarVisual : StandardNodeVisual
///     {
///         protected override void AddBox(NodeVisualContext context)
///         {
///             base.AddBox(context);                             // the filled, bordered box
///             var box = context.Layout.Box;
///             context.Add(new RectPrimitive(new Rect(box.Left, box.Top, box.Width, 4), Color.Orange, null, 0, 0, context.Node));
///         }
///     }
///     </code>
/// </example>
public class StandardNodeVisual : INodeVisual
{
    /// <summary>
    ///     The smallest font size a title is shrunk to when it does not fit the box.
    /// </summary>
    private const double MinimumTitleFontSize = 8;

    /// <summary>
    ///     The number of concentric rings a node's glow is made of (6); a protected constant, not part of the public instance API, for derived visuals that draw their own glow.
    /// </summary>
    protected const int GlowRings = 6;

    /// <summary>
    ///     Gets a shared instance of the standard visual.
    /// </summary>
    public static StandardNodeVisual Instance { get; } = new();

    /// <summary>
    ///     Initializes a new instance of the <see cref="StandardNodeVisual"/> class.
    /// </summary>
    public StandardNodeVisual()
    {
        // Nothing to initialize: the visual is stateless
    }

    /// <summary>
    ///     Adds all the parts of a standard node in drawing order: glow, box, title, icon, name, summary and ports.
    /// </summary>
    /// <remarks>
    ///     Virtual, so a derived visual can wrap it: call <c>base.Build(context)</c> and then add decoration on top, or add
    ///     something first and then call the base. Each part is a protected virtual method that can be overridden on its own.
    /// </remarks>
    /// <param name="context">The node, its layout, the theme and the sink for primitives.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="context"/> is null.</exception>
    public virtual void Build(NodeVisualContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        AddGlow(context);
        AddBox(context);
        AddTitle(context);
        AddIcon(context);
        AddName(context);
        AddSummary(context);
        AddPorts(context);
    }

    /// <summary>
    ///     Adds the soft halo around the box when the node's style has a <see cref="NodeStyle.Glow"/>.
    /// </summary>
    /// <remarks>
    ///     The halo is <see cref="GlowRings"/> concentric translucent rounded outlines that fade outward,
    ///     drawn below the box so only the part outside it shows. It reaches
    ///     <c>GlowRings * 2</c> diagram units beyond the box.
    /// </remarks>
    /// <param name="context">The visual context.</param>
    protected virtual void AddGlow(NodeVisualContext context)
    {
        if (context.Style.Glow is not { } glow)
        {
            return;
        }

        // Each ring is 2 units wide and weaker than the one inside it, so the halo fades smoothly
        for (var ring = 0; ring < GlowRings; ring++)
        {
            var fade = 1.0 - ((double)ring / GlowRings);
            var color = context.Faded(glow with { A = (byte)Math.Round(glow.A * 0.5 * fade * fade) });
            context.Add(new RectPrimitive(
                context.Layout.Box.Inflate(1 + (2 * ring)),
                null,
                color,
                2,
                context.Theme.NodeCornerRadius + 1 + (2 * ring)));
        }
    }

    /// <summary>
    ///     Adds the filled, bordered node box, using the node's style where it sets the fill, border or border thickness.
    /// </summary>
    /// <param name="context">The visual context.</param>
    protected virtual void AddBox(NodeVisualContext context)
    {
        context.Add(new RectPrimitive(
            context.Layout.Box,
            context.Fill,
            context.Border,
            context.BorderThickness,
            context.Theme.NodeCornerRadius));
    }

    /// <summary>
    ///     Adds the node type title in the header band.
    /// </summary>
    /// <remarks>
    ///     A title wider than the box wraps onto further lines and, when a single word is still too
    ///     wide, shrinks (down to a legible minimum) so it stays inside the box.
    /// </remarks>
    /// <param name="context">The visual context.</param>
    protected virtual void AddTitle(NodeVisualContext context)
    {
        var title = context.Layout.Title;
        if (title.Text.Length == 0)
        {
            return;
        }

        // Start with the theme's size and shrink until the wrapped title fits the box width
        var box = context.Layout.Box;
        var maxWidth = Math.Max(1, box.Width - (2 * context.Theme.TextGap) - 2);
        var style = context.HeaderTextStyle;
        var lines = TextWrapper.Wrap(title.Text, maxWidth, style, context.TextMeasurer);
        while (style.FontSize > MinimumTitleFontSize && (lines.Count > 2 || lines.Any(l => context.TextMeasurer.Measure(l, style).Width > maxWidth)))
        {
            style = style with { FontSize = style.FontSize - 0.5 };
            lines = TextWrapper.Wrap(title.Text, maxWidth, style, context.TextMeasurer);
        }

        // A single line keeps the whole header band as its rectangle so it is centered in it
        if (lines.Count <= 1)
        {
            context.Add(new TextPrimitive(title.Text, title.Bounds, style));
            return;
        }

        // Several lines stack from the top, centered in the band but never above the box edge
        var lineHeight = context.TextMeasurer.Measure(lines[0], style).Height;
        var y = box.Top + Math.Max(2, (context.Theme.HeaderHeight - (lines.Count * lineHeight)) / 2);
        foreach (var line in lines)
        {
            context.Add(new TextPrimitive(line, new Rect(box.Left, y, box.Width, lineHeight), style));
            y += lineHeight;
        }
    }

    /// <summary>
    ///     Adds the node type icon, when the type has one, tinted and faded as the node's style says.
    /// </summary>
    /// <param name="context">The visual context.</param>
    protected virtual void AddIcon(NodeVisualContext context)
    {
        if (context.Node.Type.Icon is { } icon && context.Layout.IconBounds is { IsEmpty: false } bounds)
        {
            context.Add(new IconPrimitive(icon, bounds, null, context.Style.IconTint, context.Style.Opacity ?? 1));
        }
    }

    /// <summary>
    ///     Adds the instance name above the box.
    /// </summary>
    /// <param name="context">The visual context.</param>
    protected virtual void AddName(NodeVisualContext context)
    {
        var name = context.Layout.Name;
        context.Add(new TextPrimitive(name.Text, name.Bounds, context.Faded(context.Theme.NameTextStyle)));
    }

    /// <summary>
    ///     Adds the property summary lines below the box.
    /// </summary>
    /// <param name="context">The visual context.</param>
    protected virtual void AddSummary(NodeVisualContext context)
    {
        foreach (var line in context.Layout.SummaryLines)
        {
            context.Add(new TextPrimitive(line.Text, line.Bounds, context.Faded(context.Theme.SummaryTextStyle)));
        }
    }

    /// <summary>
    ///     Adds each port's colored triangle and label.
    /// </summary>
    /// <param name="context">The visual context.</param>
    protected virtual void AddPorts(NodeVisualContext context)
    {
        var theme = context.Theme;
        foreach (var port in context.Layout.Ports)
        {
            // The triangle points right, the direction data flows for both inputs and outputs
            var triangle = port.Triangle;
            context.Add(new PathPrimitive(
                [
                    PathCommand.MoveTo(new Point(triangle.Left, triangle.Top)),
                    PathCommand.LineTo(new Point(triangle.Right, triangle.Top + (triangle.Height / 2))),
                    PathCommand.LineTo(new Point(triangle.Left, triangle.Bottom)),
                    PathCommand.Close(),
                ],
                context.Faded(context.PortColor(port.Port)),
                null));

            // Input labels end at their rectangle's right edge, output labels start at its left edge
            var alignment = port.Port.Direction == PortDirection.Input ? TextAlignment.Right : TextAlignment.Left;
            context.Add(new TextPrimitive(port.Label.Text, port.Label.Bounds, context.Faded(theme.PortLabelTextStyle with { Alignment = alignment })));
        }
    }
}
