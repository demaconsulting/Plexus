namespace DemaConsulting.Plexus.Tests;

using DemaConsulting.Plexus.Geometry;
using DemaConsulting.Plexus.Scene;

/// <summary>
///     A deterministic text measurer for tests: every character is 10 wide and every line 12 high.
/// </summary>
internal sealed class FixedTextMeasurer : ITextMeasurer
{
    /// <inheritdoc />
    public Size Measure(string text, TextStyle style) => new(text.Length * 10, 12);
}

/// <summary>
///     A node visual for tests that replaces the standard template with a single marker rectangle and text.
/// </summary>
internal sealed class MarkerNodeVisual : INodeVisual
{
    /// <inheritdoc />
    public void Build(NodeVisualContext context)
    {
        // Deliberately omit the element so the builder has to attribute the primitives to the node
        context.Add(new RectPrimitive(context.Layout.Box, Color.Parse("#123456"), null));
        context.Add(new TextPrimitive("custom", context.Layout.Box, context.Theme.HeaderTextStyle));
    }
}

/// <summary>
///     A node visual for tests that decorates the standard template with one extra primitive.
/// </summary>
internal sealed class DecoratingNodeVisual : StandardNodeVisual
{
    /// <inheritdoc />
    public override void Build(NodeVisualContext context)
    {
        base.Build(context);
        context.Add(new RectPrimitive(context.Layout.Box.Inflate(6), null, Color.Parse("#FF00FF")));
    }
}

/// <summary>
///     Shared fixtures and helpers for the scene tests.
/// </summary>
internal static class SceneTestModel
{
    /// <summary>
    ///     Creates a diagram with the standard test types plus an iconic node type and nodes with custom visuals.
    /// </summary>
    /// <returns>The diagram.</returns>
    public static Diagram CreateDiagram()
    {
        var types = TestModel.CreateTypes();
        types.Add(new NodeType("acme.Iconic")
        {
            Title = "ICONIC",
            Icon = IconSource.FromSvg("<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"10\" height=\"10\"/>"),
            Inputs = { new PortDefinition("in", "i32") },
            Outputs = { new PortDefinition("out", "i32") },
        });
        types.Add(new NodeType("acme.Custom") { Visual = new MarkerNodeVisual(), Inputs = { new PortDefinition("in", "f32") } });
        types.Add(new NodeType("acme.Decorated") { Visual = new DecoratingNodeVisual(), Inputs = { new PortDefinition("in", "f32") } });
        return new Diagram(types);
    }

    /// <summary>
    ///     Creates a diagram with one page.
    /// </summary>
    /// <returns>The diagram and page.</returns>
    public static (Diagram Diagram, DiagramPage Page) CreatePage()
    {
        var diagram = CreateDiagram();
        return (diagram, diagram.Pages.Add("Main"));
    }

    /// <summary>
    ///     Selects the primitives of one kind that belong to an element.
    /// </summary>
    /// <typeparam name="T">The primitive kind.</typeparam>
    /// <param name="scene">The scene.</param>
    /// <param name="element">The owning element.</param>
    /// <returns>The primitives in drawing order.</returns>
    public static List<T> For<T>(DiagramScene scene, DiagramElement element)
        where T : ScenePrimitive =>
        [.. scene.Primitives.OfType<T>().Where(p => ReferenceEquals(p.Element, element))];

    /// <summary>
    ///     Builds a scene without the grid, so primitive counts only reflect content.
    /// </summary>
    /// <param name="page">The page.</param>
    /// <returns>The scene.</returns>
    public static DiagramScene BuildNoGrid(DiagramPage page) =>
        SceneBuilder.Build(page, new SceneOptions { ShowGrid = false });
}
