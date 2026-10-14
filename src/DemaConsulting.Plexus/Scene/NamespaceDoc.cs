namespace DemaConsulting.Plexus.Scene;

/// <summary>
///     Appearance and geometry: themes, styles, node visuals, painter-agnostic scenes and hit testing.
/// </summary>
/// <example>
///     Restyle a diagram without touching its content: customize the theme, then style wires and nodes from their data
///     (<c>diagram</c>, <c>types</c> and <c>page</c> are the objects of the <c>DemaConsulting.Plexus</c> namespace example).
///     <code>
///     using DemaConsulting.Plexus;
///     using DemaConsulting.Plexus.Geometry;
///     using DemaConsulting.Plexus.Scene;
///
///     // The theme is an immutable record: copy it with `with`
///     diagram.Theme = DiagramTheme.Classic with { NodeFill = Color.Parse("#223344"), WireThickness = 3 };
///
///     // Stylers return a style (or null to keep the theme) for every wire or node, so they follow the model automatically
///     diagram.WireStyler = WireStylers.Combine(
///         WireStylers.ByPortType(),                                   // wire color = color of its port type
///         WireStylers.When(w =&gt; w.Source.Node.Name == "A", new WireStyle { Dash = [6, 4] }));
///     diagram.NodeStyler = NodeStylers.ByProperty(
///         "Value",
///         v =&gt; v is double d &amp;&amp; d &gt; 3 ? new NodeStyle { Fill = Color.Parse("#552222") } : null);
///     </code>
///     <para>
///         For a fully custom node look, implement <see cref="INodeVisual"/> (or derive from
///         <see cref="StandardNodeVisual"/>) and assign it to <see cref="NodeType.Visual"/>; it only adds
///         painter-agnostic primitives, so it works in every painter:
///     </para>
///     <code>
///     using DemaConsulting.Plexus;
///     using DemaConsulting.Plexus.Geometry;
///     using DemaConsulting.Plexus.Scene;
///
///     types.Add(new NodeType("demo.Badge") { Title = "BADGE", Visual = new BadgeVisual() });
///
///     // Scenes are what painters draw and what hit testing reads: build one, then ask what is under a point
///     var scene = SceneBuilder.Build(page, new SceneOptions { ShowGrid = false });
///     var hit = scene.HitTest(new Point(30, 40), tolerance: 4);
///     if (hit.Kind == HitKind.Node)
///     {
///         Console.WriteLine(hit.Node!.Name);
///     }
///
///     // A visual that keeps the standard look and adds a red badge in the top-right corner
///     public sealed class BadgeVisual : INodeVisual
///     {
///         public void Build(NodeVisualContext context)
///         {
///             StandardNodeVisual.Instance.Build(context);
///             var box = context.Layout.Box;
///             context.Add(new RectPrimitive(new Rect(box.Right - 14, box.Top - 4, 18, 18), Color.Red, null, 0, 9, context.Node));
///         }
///     }
///     </code>
///     <para>
///         The flow is model, then <see cref="SceneBuilder.Build"/>, then <see cref="DiagramScene"/> (an ordered list
///         of <see cref="ScenePrimitive"/>s such as <see cref="RectPrimitive"/>, <see cref="PathPrimitive"/>,
///         <see cref="TextPrimitive"/> and <see cref="IconPrimitive"/>), then a painter. The headless renderer and the
///         Avalonia editor paint the same scene, and <see cref="DiagramScene.HitTest"/> answers from the very geometry that
///         was drawn. <see cref="DiagramTheme"/> carries every metric, color and font; <see cref="WireStyle"/> and
///         <see cref="NodeStyle"/> override it per wire and per node (members left null fall back to the theme).
///         The painters live in other packages: <c>DemaConsulting.Plexus.Rendering</c> (PNG files, no UI) and <c>DemaConsulting.Plexus.Avalonia</c> (the interactive editor).
///     </para>
///     <para>
///         The main extension points are <see cref="Diagram.WireStyler"/> and <see cref="Diagram.NodeStyler"/> (compose them with
///         <see cref="WireStylers"/> and <see cref="NodeStylers"/>), <see cref="NodeType.Visual"/> for custom node drawing
///         and <see cref="ITextMeasurer"/> for text sizing (<see cref="ApproximateTextMeasurer"/> needs no fonts; the
///         rendering and Avalonia packages measure real text). Styling is configuration: it is not part of the
///         saved JSON, so pass it in a <see cref="DiagramOptions"/> to every diagram you create or load.
///     </para>
/// </example>
internal static class NamespaceDoc
{
}
