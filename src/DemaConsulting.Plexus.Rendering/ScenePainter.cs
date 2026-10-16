using System.Numerics;
using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.Drawing;
using DemaConsulting.CanvasNet.Geometry;
using DemaConsulting.CanvasNet.Rendering;
using DemaConsulting.Plexus.Scene;
using CanvasPath = DemaConsulting.CanvasNet.Geometry.Path;
using PathCommand = DemaConsulting.Plexus.Scene.PathCommand;
using PathCommandKind = DemaConsulting.Plexus.Scene.PathCommandKind;
using PlexusColor = DemaConsulting.Plexus.Geometry.Color;
using PlexusPoint = DemaConsulting.Plexus.Geometry.Point;
using PlexusRect = DemaConsulting.Plexus.Geometry.Rect;
using SceneTextAlignment = DemaConsulting.Plexus.Scene.TextAlignment;

namespace DemaConsulting.Plexus.Rendering;

/// <summary>
///     Paints painter-agnostic scene primitives onto a CanvasNet pixel surface.
/// </summary>
/// <remarks>
///     The painter maps diagram coordinates to pixels with a fixed origin offset and a uniform
///     scale, and draws every primitive kind with anti-aliasing: rectangles (optionally rounded and
///     stroked), paths (filled and/or stroked, curves included), text, SVG icons (rasterized once,
///     cached, composited) and dot grids. A rectangle outline whose stroke thickness times the scale is
///     a whole number of output pixels (at least one) is snapped to the pixel grid, so a 1-pixel border
///     (thickness 1 at scale 1, or thickness 0.5 at scale 2) and a 2-pixel border (thickness 1 at
///     scale 2) stay crisp instead of smearing across extra pixel rows. A painter instance is tied to
///     one surface and is not thread-safe.
/// </remarks>
internal sealed class ScenePainter
{
    /// <summary>
    ///     The tolerance under which a device stroke width counts as a whole number of pixels.
    /// </summary>
    private const double WholePixelTolerance = 0.01;

    /// <summary>
    ///     The CanvasNet drawing facade that fills and strokes onto the surface.
    /// </summary>
    private readonly Canvas _canvas;

    /// <summary>
    ///     The surface being painted.
    /// </summary>
    private readonly Surface _surface;

    /// <summary>
    ///     The diagram-space point that maps to the surface's top-left pixel.
    /// </summary>
    private readonly PlexusPoint _origin;

    /// <summary>
    ///     The number of pixels per diagram unit.
    /// </summary>
    private readonly double _scale;

    /// <summary>
    ///     Initializes a new instance of the <see cref="ScenePainter"/> class.
    /// </summary>
    /// <param name="surface">The surface to paint onto. Must not be null.</param>
    /// <param name="origin">The diagram-space point that maps to the surface's top-left pixel.</param>
    /// <param name="scale">The pixels per diagram unit; finite and greater than zero.</param>
    public ScenePainter(Surface surface, PlexusPoint origin, double scale)
    {
        ArgumentNullException.ThrowIfNull(surface);
        _surface = surface;
        _canvas = new Canvas(surface);
        _origin = origin;
        _scale = scale;
    }

    /// <summary>
    ///     Paints primitives in the given (z) order.
    /// </summary>
    /// <param name="primitives">The primitives, back to front. Must not be null.</param>
    public void Paint(IEnumerable<ScenePrimitive> primitives)
    {
        ArgumentNullException.ThrowIfNull(primitives);
        foreach (var primitive in primitives)
        {
            Paint(primitive);
        }
    }

    /// <summary>
    ///     Paints one primitive.
    /// </summary>
    /// <param name="primitive">The primitive. Must not be null.</param>
    /// <exception cref="NotSupportedException">Thrown for a primitive kind this painter does not know.</exception>
    public void Paint(ScenePrimitive primitive)
    {
        ArgumentNullException.ThrowIfNull(primitive);

        // Primitives are a closed set owned by Core; anything else means Core grew a kind
        // that this painter must learn, which should fail loudly rather than vanish silently
        switch (primitive)
        {
            case RectPrimitive rect:
                PaintRect(rect);
                break;
            case PathPrimitive path:
                PaintPath(path);
                break;
            case TextPrimitive text:
                PaintText(text);
                break;
            case IconPrimitive icon:
                PaintIcon(icon);
                break;
            case GridPrimitive grid:
                PaintGrid(grid);
                break;
            default:
                throw new NotSupportedException($"Scene primitive '{primitive.GetType().Name}' is not supported by the PNG painter.");
        }
    }

    /// <summary>
    ///     Converts a diagram color to a CanvasNet pixel value.
    /// </summary>
    /// <param name="color">The diagram color.</param>
    /// <returns>The equivalent pixel.</returns>
    internal static Rgba32 ToPixel(PlexusColor color) => new(color.R, color.G, color.B, color.A);

    /// <summary>
    ///     Blends a color over a pixel using straight-alpha "over" compositing.
    /// </summary>
    /// <param name="destination">The pixel to update.</param>
    /// <param name="color">The source color.</param>
    /// <param name="coverage">An extra opacity factor in [0, 1] (for example anti-aliasing coverage).</param>
    private static void Blend(ref Rgba32 destination, Rgba32 color, double coverage)
    {
        // Effective source opacity combines the color's own alpha with the coverage
        var sourceAlpha = color.A / 255.0 * Math.Clamp(coverage, 0, 1);
        if (sourceAlpha <= 0)
        {
            return;
        }

        // Standard Porter-Duff over for straight (unassociated) alpha
        var destinationAlpha = destination.A / 255.0;
        var outAlpha = sourceAlpha + (destinationAlpha * (1 - sourceAlpha));
        if (outAlpha <= 0)
        {
            return;
        }

        byte Channel(byte source, byte backdrop) =>
            (byte)Math.Clamp(Math.Round(((source * sourceAlpha) + (backdrop * destinationAlpha * (1 - sourceAlpha))) / outAlpha), 0, 255);

        destination = new Rgba32(
            Channel(color.R, destination.R),
            Channel(color.G, destination.G),
            Channel(color.B, destination.B),
            (byte)Math.Clamp(Math.Round(outAlpha * 255), 0, 255));
    }

    /// <summary>
    ///     Maps a diagram point to device (pixel) coordinates.
    /// </summary>
    /// <param name="point">The diagram point.</param>
    /// <returns>The device position.</returns>
    private Vector2 ToDevice(PlexusPoint point) =>
        new((float)((point.X - _origin.X) * _scale), (float)((point.Y - _origin.Y) * _scale));

    /// <summary>
    ///     Snaps a device coordinate so a stroke of the given whole-pixel width is pixel-aligned.
    /// </summary>
    /// <param name="value">The device coordinate of the stroke's center line.</param>
    /// <param name="strokePixels">The stroke width in output pixels (thickness times scale).</param>
    /// <returns>An odd width centers on a pixel middle, an even width on a pixel boundary.</returns>
    private static double Snap(double value, double strokePixels) =>
        (long)Math.Round(strokePixels) % 2 == 1 ? Math.Floor(value) + 0.5 : Math.Round(value);

    /// <summary>
    ///     Builds a stroke style for a device-space width.
    /// </summary>
    /// <param name="widthPixels">The stroke width in device pixels.</param>
    /// <param name="join">The join style.</param>
    /// <returns>The style.</returns>
    private static StrokeStyle Stroke(double widthPixels, LineJoin join) =>
        new((float)Math.Max(widthPixels, 0.25), LineCap.Butt, join, 4f, [], 0f);

    /// <summary>
    ///     Paints a (possibly rounded, filled and/or stroked) rectangle.
    /// </summary>
    /// <param name="primitive">The rectangle primitive.</param>
    private void PaintRect(RectPrimitive primitive)
    {
        // Convert to device space, snapping the outline when it carries a crisp whole-pixel stroke
        var rect = primitive.Rect;
        double left = (rect.Left - _origin.X) * _scale;
        double top = (rect.Top - _origin.Y) * _scale;
        double right = (rect.Right - _origin.X) * _scale;
        double bottom = (rect.Bottom - _origin.Y) * _scale;
        var strokePixels = primitive.StrokeThickness * _scale;
        if (primitive.Stroke is not null && Math.Abs(strokePixels - Math.Round(strokePixels)) < WholePixelTolerance && strokePixels >= 1)
        {
            (left, top, right, bottom) = (Snap(left, strokePixels), Snap(top, strokePixels), Snap(right, strokePixels), Snap(bottom, strokePixels));
        }

        var width = (float)(right - left);
        var height = (float)(bottom - top);
        if (width <= 0 || height <= 0)
        {
            return;
        }

        // Rounded and square corners share one code path: a zero radius is a plain rectangle
        var radius = (float)(primitive.CornerRadius * _scale);
        var shape = radius > 0
            ? CanvasPath.RoundRectangle((float)left, (float)top, width, height, radius)
            : CanvasPath.Rectangle((float)left, (float)top, width, height);
        if (primitive.Fill is { } fill)
        {
            _canvas.FillPath(shape, ToPixel(fill), FillRule.NonZero);
        }

        if (primitive.Stroke is { } stroke && primitive.StrokeThickness > 0)
        {
            _canvas.StrokePath(shape, Stroke(strokePixels, LineJoin.Miter), ToPixel(stroke));
        }
    }

    /// <summary>
    ///     Paints a path primitive (lines and cubic curves, filled and/or stroked).
    /// </summary>
    /// <param name="primitive">The path primitive.</param>
    private void PaintPath(PathPrimitive primitive)
    {
        if (primitive.Commands.Count == 0)
        {
            return;
        }

        if (primitive.Fill is { } fill)
        {
            // Fills always treat figures as closed, as vector drawing APIs conventionally do
            _canvas.FillPath(BuildPath(primitive.Commands, closeFigures: true), ToPixel(fill), FillRule.NonZero);
        }

        if (primitive.Stroke is { } stroke && primitive.StrokeThickness > 0)
        {
            // Round joins keep rounded wire corners smooth; butt caps end wires flush with ports
            _canvas.StrokePath(
                BuildPath(primitive.Commands, closeFigures: false),
                Stroke(primitive.StrokeThickness * _scale, LineJoin.Round),
                ToPixel(stroke));
        }
    }

    /// <summary>
    ///     Converts scene path commands to a CanvasNet path in device space.
    /// </summary>
    /// <param name="commands">The commands.</param>
    /// <param name="closeFigures">True to close every figure (for filling).</param>
    /// <returns>The path.</returns>
    private CanvasPath BuildPath(IReadOnlyList<PathCommand> commands, bool closeFigures)
    {
        var builder = new PathBuilder();
        var hasFigure = false;
        var isOpen = false;
        var start = Vector2.Zero;
        foreach (var command in commands)
        {
            switch (command.Kind)
            {
                case PathCommandKind.MoveTo:
                    if (closeFigures && isOpen)
                    {
                        builder.Close();
                    }

                    start = ToDevice(command.End);
                    builder.MoveTo(start);
                    (hasFigure, isOpen) = (true, true);
                    break;
                case PathCommandKind.LineTo:
                    // A drawing command after a close (or with no figure yet) implicitly starts a figure
                    StartFigureIfNeeded();
                    builder.LineTo(ToDevice(command.End));
                    break;
                case PathCommandKind.CubicTo:
                    StartFigureIfNeeded();
                    builder.CubicBezierTo(ToDevice(command.Control1), ToDevice(command.Control2), ToDevice(command.End));
                    break;
                default:
                    if (isOpen)
                    {
                        builder.Close();
                        isOpen = false;
                    }

                    break;
            }
        }

        if (closeFigures && isOpen)
        {
            builder.Close();
        }

        return builder.Build();

        void StartFigureIfNeeded()
        {
            if (!isOpen)
            {
                builder.MoveTo(hasFigure ? start : Vector2.Zero);
                (hasFigure, isOpen) = (true, true);
            }
        }
    }

    /// <summary>
    ///     Paints a single line of text.
    /// </summary>
    /// <param name="primitive">The text primitive.</param>
    private void PaintText(TextPrimitive primitive)
    {
        var style = primitive.Style;
        var size = style.FontSize * _scale;
        if (primitive.Text.Length == 0 || size <= 0 || !double.IsFinite(size))
        {
            return;
        }

        // The rectangle fixes the anchor: its left edge, center or right edge per alignment
        var rect = primitive.Rect;
        var anchorX = style.Alignment switch
        {
            SceneTextAlignment.Center => rect.Left + (rect.Width / 2),
            SceneTextAlignment.Right => rect.Right,
            _ => rect.Left,
        };
        var align = style.Alignment switch
        {
            SceneTextAlignment.Center => TextAlign.Center,
            SceneTextAlignment.Right => TextAlign.Right,
            _ => TextAlign.Left,
        };

        // The line is centered vertically in the rectangle; the baseline is rounded to a whole
        // pixel so glyph bottoms stay crisp
        var font = FontProvider.Get(style.FontFamily, style.Bold);
        var metrics = TextRenderer.MeasureText(primitive.Text, font, (float)size);
        var deviceTop = (rect.Top - _origin.Y) * _scale;
        var deviceHeight = rect.Height * _scale;
        var baseline = Math.Round(deviceTop + ((deviceHeight - (metrics.Ascent + metrics.Descent)) / 2) + metrics.Ascent);
        _canvas.DrawText(
            primitive.Text,
            (float)((anchorX - _origin.X) * _scale),
            (float)baseline,
            align,
            font,
            (float)size,
            ToPixel(style.Color));
    }

    /// <summary>
    ///     Paints an SVG icon from the shared cache.
    /// </summary>
    /// <remarks>
    ///     A tinted icon is painted as a silhouette: each pixel takes the tint's color and the coverage
    ///     is the icon pixel's alpha times the tint's alpha; the primitive's opacity then scales it all.
    ///     The cached rasterization is shared and never changed, so tinting costs only this loop.
    /// </remarks>
    /// <param name="primitive">The icon primitive.</param>
    private void PaintIcon(IconPrimitive primitive)
    {
        // Icons are bitmaps at a whole pixel size and position; the SVG scales to fit the box
        var x = (int)Math.Round((primitive.Rect.Left - _origin.X) * _scale);
        var y = (int)Math.Round((primitive.Rect.Top - _origin.Y) * _scale);
        var width = (int)Math.Round(primitive.Rect.Width * _scale);
        var height = (int)Math.Round(primitive.Rect.Height * _scale);
        if (width < 1 || height < 1 || width > Surface.MaxDimension || height > Surface.MaxDimension)
        {
            return;
        }

        var icon = IconCache.Shared.Get(primitive.Icon, width, height);
        if (icon is null)
        {
            return;
        }

        // Composite row by row, clipped to the surface; a tint replaces the color, keeping the icon's shape as coverage
        var tint = primitive.Tint is { } tintColor ? ToPixel(tintColor) : (Rgba32?)null;
        for (var row = 0; row < height; row++)
        {
            var targetY = y + row;
            if (targetY < 0 || targetY >= _surface.Height)
            {
                continue;
            }

            var source = icon.GetRowSpan(row);
            var target = _surface.GetRowSpan(targetY);
            for (var column = 0; column < width; column++)
            {
                var targetX = x + column;
                if (targetX >= 0 && targetX < target.Length && source[column].A > 0)
                {
                    var pixel = source[column];
                    if (tint is { } silhouette)
                    {
                        pixel = new Rgba32(silhouette.R, silhouette.G, silhouette.B, (byte)Math.Round(pixel.A * silhouette.A / 255.0));
                    }

                    Blend(ref target[targetX], pixel, primitive.Opacity);
                }
            }
        }
    }

    /// <summary>
    ///     Paints a dot grid directly into pixels, which is far cheaper than filling a path per dot.
    /// </summary>
    /// <param name="primitive">The grid primitive.</param>
    private void PaintGrid(GridPrimitive primitive)
    {
        // Dots sit on multiples of the spacing (from the diagram origin) so they line up with
        // grid-snapped nodes, restricted to the grid's area and the surface
        var area = primitive.Rect;
        var spacing = primitive.Spacing;
        var color = ToPixel(primitive.Color);
        var dot = Math.Max(1, primitive.DotSize * _scale);
        var firstColumn = (long)Math.Ceiling(area.Left / spacing);
        var lastColumn = (long)Math.Floor(area.Right / spacing);
        var firstRow = (long)Math.Ceiling(area.Top / spacing);
        var lastRow = (long)Math.Floor(area.Bottom / spacing);
        for (var row = firstRow; row <= lastRow; row++)
        {
            var dotY = Math.Round(((row * spacing) - _origin.Y) * _scale);
            if (dotY + dot < 0 || dotY >= _surface.Height)
            {
                continue;
            }

            for (var column = firstColumn; column <= lastColumn; column++)
            {
                var dotX = Math.Round(((column * spacing) - _origin.X) * _scale);
                if (dotX + dot >= 0 && dotX < _surface.Width)
                {
                    PaintDot(dotX, dotY, dot, color);
                }
            }
        }
    }

    /// <summary>
    ///     Paints one anti-aliased square dot whose top-left corner is at a whole pixel.
    /// </summary>
    /// <param name="x">The left pixel column.</param>
    /// <param name="y">The top pixel row.</param>
    /// <param name="size">The dot edge length in pixels (may be fractional).</param>
    /// <param name="color">The dot color.</param>
    private void PaintDot(double x, double y, double size, Rgba32 color)
    {
        // Each covered pixel receives the fraction of its area the dot overlaps
        var columns = (int)Math.Ceiling(size);
        for (var dy = 0; dy < columns; dy++)
        {
            var pixelY = (int)y + dy;
            if (pixelY < 0 || pixelY >= _surface.Height)
            {
                continue;
            }

            var verticalCoverage = Math.Min(1, size - dy);
            var row = _surface.GetRowSpan(pixelY);
            for (var dx = 0; dx < columns; dx++)
            {
                var pixelX = (int)x + dx;
                if (pixelX >= 0 && pixelX < row.Length)
                {
                    Blend(ref row[pixelX], color, verticalCoverage * Math.Min(1, size - dx));
                }
            }
        }
    }
}
