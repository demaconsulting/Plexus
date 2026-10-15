using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.Plexus.Geometry;
using DemaConsulting.Plexus.Scene;

namespace DemaConsulting.Plexus.Rendering.Tests;

/// <summary>
///     Rendering tests for dynamic node styling: a styled node must show up in the PNG with its fill, border, halo,
///     fade and tinted icon, and a tint must recolor an icon exactly as documented (a silhouette).
/// </summary>
public class NodeStyleRenderingTests
{
    /// <summary>
    ///     Options that render the area (0, 0, 300, 200) without a grid at scale 1, so pixels equal diagram units.
    /// </summary>
    private static readonly RenderOptions Area = new() { Bounds = new Rect(0, 0, 300, 200), ShowGrid = false };

    /// <summary>
    ///     Proves at system level that a page with a styled and an unstyled node renders the style where the styler decides and the theme everywhere else.
    /// </summary>
    [Fact]
    public void PlexusRendering_Render_StyledNodes_ShowTheStyleOnlyWhereTheStylerDecides()
    {
        // Arrange: two icon nodes, only the first is styled (fill and icon tint), then restyled after a host refresh
        var (diagram, page) = RenderTestModel.CreatePage();
        var styledNode = page.AddNode("t.Icon", 30, 30, "A");
        var plainNode = page.AddNode("t.Icon", 170, 30, "B");
        var tint = Color.Parse("#FFFF00");
        diagram.NodeStyler = node => ReferenceEquals(node, styledNode) ? new NodeStyle { Fill = Color.Parse("#204060"), IconTint = tint } : null;
        var scene = SceneBuilder.Build(page, new SceneOptions { ShowGrid = false });
        var styledIcon = scene.Primitives.OfType<IconPrimitive>().Single(i => ReferenceEquals(i.Element, styledNode)).Rect.Center;
        var plainIcon = scene.Primitives.OfType<IconPrimitive>().Single(i => ReferenceEquals(i.Element, plainNode)).Rect.Center;

        // Act
        using var first = PlexusRenderer.Render(page, Area);
        tint = Color.Parse("#00FFFF");
        diagram.RefreshAppearance();
        using var second = PlexusRenderer.Render(page, Area);

        // Assert
        Assert.Multiple(
            () => Assert.Equal(RenderTestModel.Pixel(Color.Parse("#204060")), first[(int)styledNode.Bounds.Left + 3, (int)styledNode.Bounds.Bottom - 4]),
            () => Assert.Equal(RenderTestModel.Pixel(DiagramTheme.Classic.NodeFill), first[(int)plainNode.Bounds.Left + 3, (int)plainNode.Bounds.Bottom - 4]),
            () => Assert.Equal(RenderTestModel.Pixel(Color.Parse("#FFFF00")), first[(int)styledIcon.X, (int)styledIcon.Y]),
            () => Assert.Equal(RenderTestModel.Pixel(Color.Parse("#FF0000")), first[(int)plainIcon.X, (int)plainIcon.Y]),
            () => Assert.Equal(RenderTestModel.Pixel(Color.Parse("#00FFFF")), second[(int)styledIcon.X, (int)styledIcon.Y]));
    }

    /// <summary>
    ///     Proves through the public renderer that the style's fill, border and tinted icon appear in the picture while an unstyled node keeps the theme look.
    /// </summary>
    [Fact]
    public void PlexusRenderer_Render_StyledNode_ShowsFillBorderAndTintedIcon()
    {
        // Arrange: one icon node; sample the box, its border and the middle of the icon (a red square by default)
        var (diagram, page) = RenderTestModel.CreatePage();
        var node = page.AddNode("t.Icon", 40, 30, "N");
        var scene = SceneBuilder.Build(page, new SceneOptions { ShowGrid = false });
        var box = node.Bounds;
        var iconCenter = scene.Primitives.OfType<IconPrimitive>().Single().Rect.Center;
        using var plain = PlexusRenderer.Render(page, Area);
        diagram.NodeStyler = _ => new NodeStyle
        {
            Fill = Color.Parse("#204060"),
            Border = Color.Parse("#00FF00"),
            BorderThickness = 2,
            IconTint = Color.Parse("#FFFF00"),
        };

        // Act
        using var styled = PlexusRenderer.Render(page, Area);

        // Assert
        var inside = ((int)box.Left + 3, (int)box.Bottom - 4);
        var iconPixel = ((int)iconCenter.X, (int)iconCenter.Y);
        Assert.Multiple(
            () => Assert.Equal(RenderTestModel.Pixel(Color.Parse("#FF0000")), plain[iconPixel.Item1, iconPixel.Item2]),
            () => Assert.Equal(RenderTestModel.Pixel(DiagramTheme.Classic.NodeFill), plain[inside.Item1, inside.Item2]),
            () => Assert.Equal(RenderTestModel.Pixel(Color.Parse("#204060")), styled[inside.Item1, inside.Item2]),
            () => Assert.Equal(RenderTestModel.Pixel(Color.Parse("#FFFF00")), styled[iconPixel.Item1, iconPixel.Item2]),
            () => Assert.Equal(RenderTestModel.Pixel(Color.Parse("#00FF00")), styled[(int)Math.Round(box.Left), (int)box.Bottom - 8]));
    }

    /// <summary>
    ///     Proves that the glow draws a halo outside the box that fades with distance, and that opacity blends the box with the canvas.
    /// </summary>
    [Fact]
    public void PlexusRenderer_Render_GlowAndOpacity_ShowHaloAndFade()
    {
        // Arrange
        var (diagram, page) = RenderTestModel.CreatePage();
        var node = page.AddNode("t.Plain", 100, 80, "N");
        var canvas = RenderTestModel.Pixel(DiagramTheme.Classic.CanvasBackground);
        var box = node.Bounds;
        var y = (int)(box.Top + (box.Height / 2));
        diagram.NodeStyler = _ => new NodeStyle { Glow = Color.Parse("#00FF00") };

        // Act
        using var glowing = PlexusRenderer.Render(page, Area);

        // Assert: the pixel just outside the box is tinted green, a little further out is fainter, beyond the halo is the canvas
        var near = glowing[(int)box.Left - 2, y];
        var far = glowing[(int)box.Left - 11, y];
        var beyond = glowing[(int)box.Left - 16, y];
        Assert.Multiple(
            () => Assert.True(near.G - near.R > 10, $"near halo pixel {near}"),
            () => Assert.True(far.G > canvas.G - 5 && (far.R < canvas.R || far.B < canvas.B), $"far halo pixel {far}"),
            () => Assert.True(near.R < far.R, "the halo fades outward"),
            () => Assert.Equal(canvas, beyond));

        // Act: half opacity instead
        diagram.NodeStyler = _ => new NodeStyle { Fill = Color.Black, Opacity = 0.5 };
        using var faded = PlexusRenderer.Render(page, Area);

        // Assert: half black over the canvas color
        var middle = faded[(int)(box.Left + (box.Width / 2)), (int)(box.Top + (box.Height / 2))];
        Assert.Multiple(
            () => Assert.InRange(middle.R, (canvas.R / 2) - 2, (canvas.R / 2) + 2),
            () => Assert.InRange(middle.G, (canvas.G / 2) - 2, (canvas.G / 2) + 2));
    }
}
