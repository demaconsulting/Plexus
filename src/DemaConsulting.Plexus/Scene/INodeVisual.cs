namespace DemaConsulting.Plexus.Scene;

/// <summary>
///     Builds the drawing primitives of one node; the escape hatch for fully custom node appearance.
/// </summary>
/// <remarks>
///     Assign an implementation to <see cref="NodeType.Visual"/> to replace the standard node
///     template. A visual only adds primitives; it does not paint. Because primitives are
///     painter-agnostic, a custom visual works in every painter automatically. Port anchors,
///     hit testing and wire attachment keep using the node's <see cref="NodeLayout"/>, so a custom
///     visual should draw its ports where the layout puts them. Wrap or extend
///     <see cref="StandardNodeVisual"/> to decorate the standard look instead of replacing it.
///     Implementations must be deterministic and must not mutate the model.
/// </remarks>
/// <example>
///     A visual that keeps the standard look and dims nodes whose <c>Enabled</c> property is false.
///     <code>
///     using DemaConsulting.Plexus;
///     using DemaConsulting.Plexus.Geometry;
///     using DemaConsulting.Plexus.Scene;
///
///     var type = new NodeType("demo.Worker")
///     {
///         Properties = { new PropertyDefinition("Enabled", true) },
///         Visual = new DimWhenDisabledVisual(),
///     };
///
///     public sealed class DimWhenDisabledVisual : INodeVisual
///     {
///         public void Build(NodeVisualContext context)
///         {
///             StandardNodeVisual.Instance.Build(context);       // the standard box, title, icon and ports first
///             if (context.Node.Properties.TryGetValue("Enabled", out var enabled) &amp;&amp; enabled is false)
///             {
///                 // A translucent overlay over the whole node box
///                 context.Add(new RectPrimitive(context.Layout.Box, Color.FromArgb(120, 0, 0, 0), null, 0, 4, context.Node));
///             }
///         }
///     }
///     </code>
///     <para>
///         A visual written from scratch: a diamond that still shows the title, the instance name, the summary and the ports.
///         Everything is positioned from <see cref="NodeVisualContext.Layout"/> (a <see cref="NodeLayout"/>) so ports stay where wires attach.
///         Note that <c>Layout.Title.Bounds</c> is the header band along the top edge of the box, which lies outside a diamond, so the
///         title is centered in the diamond instead; <c>Layout.Name</c> and the summary lines are already outside the box; each
///         <see cref="PortLayout"/> supplies the triangle and label rectangles (the triangle always points right, the direction of data flow, and
///         input labels need right alignment). Hit testing keeps using the rectangular node box, so a click in a corner of the box
///         outside the diamond still hits the node.
///     </para>
///     <code>
///     using DemaConsulting.Plexus;
///     using DemaConsulting.Plexus.Geometry;
///     using DemaConsulting.Plexus.Scene;
///
///     var decision = new NodeType("flow.Decision")
///     {
///         Inputs = { new PortDefinition("in", "flow") },
///         Outputs = { new PortDefinition("yes", "flow"), new PortDefinition("no", "flow") },
///         Visual = new DiamondVisual(),
///     };
///
///     public sealed class DiamondVisual : INodeVisual
///     {
///         public void Build(NodeVisualContext context)
///         {
///             var layout = context.Layout;
///             var box = layout.Box;
///             var theme = context.Theme;
///
///             // 1. The shape; Fill, Border and BorderThickness already fall back to the theme and honor the node style's opacity
///             context.Add(new PathPrimitive(
///                 [
///                     PathCommand.MoveTo(new Point(box.Center.X, box.Top)),
///                     PathCommand.LineTo(new Point(box.Right, box.Center.Y)),
///                     PathCommand.LineTo(new Point(box.Center.X, box.Bottom)),
///                     PathCommand.LineTo(new Point(box.Left, box.Center.Y)),
///                     PathCommand.Close(),
///                 ],
///                 context.Fill,
///                 context.Border,
///                 context.BorderThickness));
///
///             // 2. The title, centered in the diamond rather than in the (rectangular) header band
///             var titleBand = new Rect(box.Left, box.Center.Y - 8, box.Width, 16);
///             context.Add(new TextPrimitive(layout.Title.Text, titleBand, context.HeaderTextStyle));
///
///             // 3. The instance name above the box and the summary lines below it
///             context.Add(new TextPrimitive(layout.Name.Text, layout.Name.Bounds, context.Faded(theme.NameTextStyle)));
///             foreach (var line in layout.SummaryLines)
///             {
///                 context.Add(new TextPrimitive(line.Text, line.Bounds, context.Faded(theme.SummaryTextStyle)));
///             }
///
///             // 4. The ports: a triangle in the port type's color and a label, exactly where the layout puts them
///             foreach (var port in layout.Ports)
///             {
///                 var t = port.Triangle;
///                 context.Add(new PathPrimitive(
///                     [
///                         PathCommand.MoveTo(new Point(t.Left, t.Top)),
///                         PathCommand.LineTo(new Point(t.Right, t.Top + (t.Height / 2))),
///                         PathCommand.LineTo(new Point(t.Left, t.Bottom)),
///                         PathCommand.Close(),
///                     ],
///                     context.Faded(context.PortColor(port.Port)),
///                     null));
///
///                 var alignment = port.Port.Direction == PortDirection.Input ? TextAlignment.Right : TextAlignment.Left;
///                 context.Add(new TextPrimitive(port.Label.Text, port.Label.Bounds, context.Faded(theme.PortLabelTextStyle with { Alignment = alignment })));
///             }
///         }
///     }
///     </code>
/// </example>
public interface INodeVisual
{
    /// <summary>
    ///     Adds the primitives that draw one node, in drawing order (later primitives paint over earlier ones).
    /// </summary>
    /// <remarks>
    ///     Called by <see cref="SceneBuilder"/> each time a scene is built for every visible node, so keep it fast, deterministic and
    ///     free of side effects. Add primitives with <see cref="NodeVisualContext.Add"/>; the selection and hover outlines are added by the
    ///     builder afterwards. Call <c>StandardNodeVisual.Instance.Build(context)</c> to start from the standard look.
    /// </remarks>
    /// <param name="context">The node, its layout, the theme and the sink for primitives.</param>
    void Build(NodeVisualContext context);
}
