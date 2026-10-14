using System.Runtime.CompilerServices;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Avalonia.Svg.Skia;
using DemaConsulting.Plexus.Scene;
using AvaloniaPoint = Avalonia.Point;
using AvaloniaRect = Avalonia.Rect;
using PlexusColor = DemaConsulting.Plexus.Geometry.Color;
using PlexusRect = DemaConsulting.Plexus.Geometry.Rect;
using TextAlignment = DemaConsulting.Plexus.Scene.TextAlignment;

namespace DemaConsulting.Plexus.Avalonia;

/// <summary>
///     Paints the primitives of a <see cref="DiagramScene"/> onto an Avalonia <see cref="DrawingContext"/>.
/// </summary>
/// <remarks>
///     The painter is deliberately dumb: the scene already decided what to draw and where, so this
///     class only maps each primitive kind onto the matching Avalonia call. Brushes, pens, path
///     geometries, formatted text and SVG icons are cached because a scene is repainted every
///     frame while it is panned or dragged. The context is expected to already carry the view
///     transform, so every coordinate here is in diagram units. UI thread only.
/// </remarks>
internal sealed class ScenePainter
{
    /// <summary>
    ///     The smallest on-screen distance between grid dots; sparser grids are thinned by powers of two.
    /// </summary>
    private const double MinScreenGridSpacing = 7;

    /// <summary>
    ///     The most entries kept in the text cache before it is emptied.
    /// </summary>
    private const int MaxTextCacheEntries = 4096;

    /// <summary>
    ///     Solid brushes by color.
    /// </summary>
    private readonly Dictionary<PlexusColor, ImmutableSolidColorBrush> _brushes = [];

    /// <summary>
    ///     Pens by color and thickness.
    /// </summary>
    private readonly Dictionary<(PlexusColor Color, double Thickness), ImmutablePen> _pens = [];

    /// <summary>
    ///     Path geometries, kept as long as their (immutable) primitive lives.
    /// </summary>
    private readonly ConditionalWeakTable<PathPrimitive, StreamGeometry> _geometries = [];

    /// <summary>
    ///     Opacity-mask brushes that tint icons, kept as long as their icon image lives.
    /// </summary>
    private readonly ConditionalWeakTable<SvgImage, DrawingBrush> _iconMasks = [];

    /// <summary>
    ///     Formatted text by string and style.
    /// </summary>
    private readonly Dictionary<(string Text, TextStyle Style), FormattedText> _texts = [];

    /// <summary>
    ///     Paints every primitive of a scene that is visible.
    /// </summary>
    /// <param name="context">The drawing context, already carrying the diagram-to-screen transform.</param>
    /// <param name="scene">The scene to paint.</param>
    /// <param name="visible">The visible region in diagram units, used to skip off-screen primitives.</param>
    /// <param name="zoom">The current zoom factor, used to keep grid dots a sensible density.</param>
    public void Paint(DrawingContext context, DiagramScene scene, PlexusRect visible, double zoom)
    {
        // Primitives are in drawing order; only the ones touching the viewport cost any work
        foreach (var primitive in scene.Primitives)
        {
            if (!primitive.Bounds.Intersects(visible))
            {
                continue;
            }

            switch (primitive)
            {
                case GridPrimitive grid:
                    PaintGrid(context, grid, visible, zoom);
                    break;
                case RectPrimitive rect:
                    PaintRect(context, rect);
                    break;
                case PathPrimitive path:
                    PaintPath(context, path);
                    break;
                case TextPrimitive text:
                    PaintText(context, text);
                    break;
                case IconPrimitive icon:
                    PaintIcon(context, icon);
                    break;
            }
        }
    }

    /// <summary>
    ///     Gets a cached solid brush for a color.
    /// </summary>
    /// <param name="color">The color.</param>
    /// <returns>The brush.</returns>
    public ImmutableSolidColorBrush GetBrush(PlexusColor color)
    {
        if (!_brushes.TryGetValue(color, out var brush))
        {
            brush = new ImmutableSolidColorBrush(color.ToAvalonia());
            _brushes[color] = brush;
        }

        return brush;
    }

    /// <summary>
    ///     Gets a cached pen with flat caps and round joins.
    /// </summary>
    /// <remarks>
    ///     Flat caps match the headless painter's butt caps, so dashed wires (emitted by the scene as
    ///     separate figures) have the same dash and gap lengths in both painters.
    /// </remarks>
    /// <param name="color">The stroke color.</param>
    /// <param name="thickness">The stroke thickness in diagram units.</param>
    /// <returns>The pen.</returns>
    public ImmutablePen GetPen(PlexusColor color, double thickness)
    {
        if (!_pens.TryGetValue((color, thickness), out var pen))
        {
            pen = new ImmutablePen(GetBrush(color), thickness, null, PenLineCap.Flat, PenLineJoin.Round);
            _pens[(color, thickness)] = pen;
        }

        return pen;
    }

    /// <summary>
    ///     Paints the dotted background grid, thinning it when zoomed far out.
    /// </summary>
    /// <param name="context">The drawing context.</param>
    /// <param name="grid">The grid primitive.</param>
    /// <param name="visible">The visible region in diagram units.</param>
    /// <param name="zoom">The zoom factor.</param>
    private void PaintGrid(DrawingContext context, GridPrimitive grid, PlexusRect visible, double zoom)
    {
        // Draw only the dots inside the viewport; a full-page grid could hold hundreds of thousands
        var area = grid.Rect;
        var left = Math.Max(area.Left, visible.Left);
        var top = Math.Max(area.Top, visible.Top);
        var right = Math.Min(area.Right, visible.Right);
        var bottom = Math.Min(area.Bottom, visible.Bottom);
        if (right < left || bottom < top)
        {
            return;
        }

        // Keep dots at least a few pixels apart by skipping rows and columns when zoomed out
        var spacing = grid.Spacing;
        while (spacing * zoom < MinScreenGridSpacing)
        {
            spacing *= 2;
        }

        // Dots sit on multiples of the spacing so the grid stays put while panning; the dot size
        // never drops below one device pixel or it would vanish at low zoom
        var brush = GetBrush(grid.Color);
        var size = Math.Max(grid.DotSize, 1 / zoom);
        var firstColumn = Math.Ceiling(left / spacing);
        var firstRow = Math.Ceiling(top / spacing);
        for (var y = firstRow * spacing; y <= bottom; y += spacing)
        {
            for (var x = firstColumn * spacing; x <= right; x += spacing)
            {
                context.FillRectangle(brush, new AvaloniaRect(x - (size / 2), y - (size / 2), size, size));
            }
        }
    }

    /// <summary>
    ///     Paints a rectangle primitive, with optional fill, stroke and rounded corners.
    /// </summary>
    /// <param name="context">The drawing context.</param>
    /// <param name="primitive">The rectangle primitive.</param>
    private void PaintRect(DrawingContext context, RectPrimitive primitive)
    {
        var brush = primitive.Fill is { } fill ? GetBrush(fill) : null;
        var pen = primitive.Stroke is { } stroke ? GetPen(stroke, primitive.StrokeThickness) : null;
        context.DrawRectangle(brush, pen, primitive.Rect.ToAvalonia(), primitive.CornerRadius, primitive.CornerRadius);
    }

    /// <summary>
    ///     Paints a path primitive of lines and cubic curves.
    /// </summary>
    /// <param name="context">The drawing context.</param>
    /// <param name="primitive">The path primitive.</param>
    private void PaintPath(DrawingContext context, PathPrimitive primitive)
    {
        var geometry = _geometries.GetValue(primitive, BuildGeometry);
        var brush = primitive.Fill is { } fill ? GetBrush(fill) : null;
        var pen = primitive.Stroke is { } stroke ? GetPen(stroke, primitive.StrokeThickness) : null;
        context.DrawGeometry(brush, pen, geometry);
    }

    /// <summary>
    ///     Translates the commands of a path primitive into an Avalonia geometry.
    /// </summary>
    /// <param name="primitive">The path primitive.</param>
    /// <returns>The geometry, filled and stroked as the primitive requests when drawn.</returns>
    private static StreamGeometry BuildGeometry(PathPrimitive primitive)
    {
        var geometry = new StreamGeometry();
        using (var stream = geometry.Open())
        {
            // Avalonia figures must be begun and ended explicitly, so track whether one is open
            var open = false;
            var filled = primitive.Fill is not null;
            foreach (var command in primitive.Commands)
            {
                switch (command.Kind)
                {
                    case PathCommandKind.MoveTo:
                        if (open)
                        {
                            stream.EndFigure(false);
                        }

                        stream.BeginFigure(command.End.ToAvalonia(), filled);
                        open = true;
                        break;
                    case PathCommandKind.LineTo when open:
                        stream.LineTo(command.End.ToAvalonia());
                        break;
                    case PathCommandKind.CubicTo when open:
                        stream.CubicBezierTo(command.Control1.ToAvalonia(), command.Control2.ToAvalonia(), command.End.ToAvalonia());
                        break;
                    case PathCommandKind.Close when open:
                        stream.EndFigure(true);
                        open = false;
                        break;
                }
            }

            if (open)
            {
                stream.EndFigure(false);
            }
        }

        return geometry;
    }

    /// <summary>
    ///     Paints a single line of text inside its rectangle, honoring the style's alignment.
    /// </summary>
    /// <param name="context">The drawing context.</param>
    /// <param name="primitive">The text primitive.</param>
    private void PaintText(DrawingContext context, TextPrimitive primitive)
    {
        if (primitive.Text.Length == 0)
        {
            return;
        }

        // Text is formatted once and reused; the cache is bounded so ever-changing text cannot grow it forever
        var key = (primitive.Text, primitive.Style);
        if (!_texts.TryGetValue(key, out var formatted))
        {
            if (_texts.Count >= MaxTextCacheEntries)
            {
                _texts.Clear();
            }

            formatted = TextFormats.Create(primitive.Text, primitive.Style);
            _texts[key] = formatted;
        }

        // Alignment is applied by hand: the rectangle is the layout box, the glyphs sit centered vertically in it
        var rect = primitive.Rect;
        var x = primitive.Style.Alignment switch
        {
            TextAlignment.Center => rect.X + ((rect.Width - formatted.Width) / 2),
            TextAlignment.Right => rect.Right - formatted.Width,
            _ => rect.X,
        };
        var y = rect.Y + ((rect.Height - formatted.Height) / 2);
        context.DrawText(formatted, new AvaloniaPoint(x, y));
    }

    /// <summary>
    ///     Paints an SVG icon scaled to fit its rectangle while keeping its aspect ratio.
    /// </summary>
    /// <remarks>
    ///     A tint paints the icon as a silhouette, as the headless painter does: a rectangle of the tint
    ///     color is drawn through an opacity mask made from the icon image, so the icon's alpha is the
    ///     coverage. The icon's opacity scales the tint's alpha, or, for an icon without a tint, goes through a
    ///     uniform opacity mask, because the SVG image is a custom drawing operation that ignores
    ///     <c>DrawingContext.PushOpacity</c>.
    /// </remarks>
    /// <param name="context">The drawing context.</param>
    /// <param name="primitive">The icon primitive.</param>
    private void PaintIcon(DrawingContext context, IconPrimitive primitive)
    {
        var image = GetIcon(primitive.Icon);
        if (image is null || image.Size.Width <= 0 || image.Size.Height <= 0)
        {
            return;
        }

        // Fit-and-center, so non-square artwork is not distorted
        var target = primitive.Rect;
        var scale = Math.Min(target.Width / image.Size.Width, target.Height / image.Size.Height);
        var width = image.Size.Width * scale;
        var height = image.Size.Height * scale;
        var destination = new AvaloniaRect(
            target.X + ((target.Width - width) / 2),
            target.Y + ((target.Height - height) / 2),
            width,
            height);

        if (primitive.Tint is { } tint)
        {
            // A tint is a silhouette: the icon's alpha masks a rectangle of the tint color, which is exactly what the
            // headless painter does per pixel; the opacity scales the tint's alpha
            var mask = _iconMasks.GetValue(image, CreateMask);
            using (context.PushOpacityMask(mask, destination))
            {
                context.DrawRectangle(GetBrush(tint with { A = (byte)Math.Round(tint.A * primitive.Opacity) }), null, destination);
            }

            return;
        }

        // An SVG image is drawn by a custom operation that ignores PushOpacity, so the fade goes through a uniform
        // opacity mask, which applies to everything drawn inside it
        if (primitive.Opacity < 1)
        {
            using (context.PushOpacityMask(GetBrush(new PlexusColor(255, 255, 255, (byte)Math.Round(255 * primitive.Opacity))), destination))
            {
                context.DrawImage(image, new AvaloniaRect(image.Size), destination);
            }

            return;
        }

        context.DrawImage(image, new AvaloniaRect(image.Size), destination);
    }

    /// <summary>
    ///     Creates the brush whose alpha channel is the icon's alpha, used as an opacity mask to tint the icon.
    /// </summary>
    /// <param name="image">The icon image.</param>
    /// <returns>A brush that stretches the icon over whatever it fills.</returns>
    private static DrawingBrush CreateMask(SvgImage image) =>
        new(new ImageDrawing { ImageSource = image, Rect = new AvaloniaRect(image.Size) }) { Stretch = Stretch.Fill };

    /// <summary>
    ///     Gets the cached image for an icon, loading it on first use.
    /// </summary>
    /// <remarks>
    ///     Delegates to the shared <see cref="PlexusIcons"/> cache, so hosts and the painter parse each icon once.
    /// </remarks>
    /// <param name="icon">The icon.</param>
    /// <returns>The image, or null when the SVG could not be loaded.</returns>
    private static SvgImage? GetIcon(IconSource icon) => PlexusIcons.GetSvgImage(icon);
}
