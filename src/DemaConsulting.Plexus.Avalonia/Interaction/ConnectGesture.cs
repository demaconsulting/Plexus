using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using DemaConsulting.Plexus.Scene;
using AvaloniaPoint = Avalonia.Point;
using PlexusColor = DemaConsulting.Plexus.Geometry.Color;
using PlexusPoint = DemaConsulting.Plexus.Geometry.Point;

namespace DemaConsulting.Plexus.Avalonia.Interaction;

/// <summary>
///     Drags a new wire out of a port and connects it to the port it is released over.
/// </summary>
/// <remarks>
///     While dragging, a preview wire follows the pointer. When the pointer is over another port the
///     connection is checked with <see cref="DiagramPage.CanConnect"/> and the preview turns green (allowed)
///     or red with the reason shown beside the pointer (rejected), so the user learns why before
///     releasing. Releasing over an allowed port connects; otherwise nothing happens. The drag may
///     start from either an output or an input, because <see cref="DiagramPage.Connect(Port, Port, IReadOnlyList{Geometry.Point}?, string?)"/> orders the two
///     ports itself.
/// </remarks>
internal sealed class ConnectGesture : EditorGesture
{
    /// <summary>
    ///     The color of the preview when the drop target accepts the connection.
    /// </summary>
    private static readonly PlexusColor AllowedColor = PlexusColor.Parse("#2E8B57");

    /// <summary>
    ///     The color of the preview when the drop target rejects the connection.
    /// </summary>
    private static readonly PlexusColor DeniedColor = PlexusColor.Parse("#D22B2B");

    /// <summary>
    ///     The port the drag started from.
    /// </summary>
    private readonly Port _start;

    /// <summary>
    ///     The port currently under the pointer, if any.
    /// </summary>
    private Port? _candidate;

    /// <summary>
    ///     The verdict for connecting to <see cref="_candidate"/>.
    /// </summary>
    private ConnectionCheck _check;

    /// <summary>
    ///     The pointer position in diagram coordinates.
    /// </summary>
    private PlexusPoint _pointer;

    /// <summary>
    ///     Initializes a new instance of the <see cref="ConnectGesture"/> class.
    /// </summary>
    /// <param name="editor">The editor.</param>
    /// <param name="startScreen">The control-relative position of the press.</param>
    /// <param name="start">The port the drag starts from.</param>
    public ConnectGesture(DiagramEditor editor, AvaloniaPoint startScreen, Port start)
        : base(editor, startScreen)
    {
        _start = start;
        _pointer = start.Anchor;
    }

    /// <summary>
    ///     Gets the verdict for connecting to the port currently under the pointer: denied, with the reason shown beside the pointer, or allowed.
    /// </summary>
    /// <remarks>
    ///     Meaningful only while a candidate port is under the pointer; otherwise it is the default (denied) value.
    /// </remarks>
    internal ConnectionCheck Verdict => _candidate is null ? default : _check;

    /// <inheritdoc />
    public override StandardCursorType? Cursor => StandardCursorType.Cross;

    /// <inheritdoc />
    public override void Move(AvaloniaPoint screen, KeyModifiers modifiers)
    {
        // Find a port under the pointer other than the one we started from and ask the model about it
        _pointer = Editor.ToDiagram(screen);
        var hit = Editor.HitTestDiagram(_pointer);
        _candidate = hit.Port is { } port && !ReferenceEquals(port, _start) ? port : null;
        _check = _candidate is not null && Editor.LivePage() is { } page ? page.CanConnect(_start, _candidate) : default;
        Editor.InvalidateVisual();
    }

    /// <inheritdoc />
    public override void Complete(AvaloniaPoint screen, KeyModifiers modifiers)
    {
        // Re-evaluate at the release position, which may differ from the last move event
        Move(screen, modifiers);
        if (_candidate is not null && _check.IsAllowed && Editor.LivePage() is { } page)
        {
            try
            {
                Editor.SetSelection([page.Connect(_start, _candidate)]);
            }
            catch (InvalidOperationException)
            {
                // The model re-checks the rules when it connects; losing a race to a change of
                // the rules since the preview is not worth an error dialog, the drop just does nothing
            }
        }

        _candidate = null;
        Editor.InvalidateVisual();
    }

    /// <inheritdoc />
    public override void Cancel()
    {
        _candidate = null;
        Editor.InvalidateVisual();
    }

    /// <inheritdoc />
    public override void Draw(DrawingContext context, ScenePainter painter, DiagramTheme theme, double zoom)
    {
        // Neutral while searching, green or red once there is a verdict
        var color = theme.SelectionColor;
        if (_candidate is not null)
        {
            color = _check.IsAllowed ? AllowedColor : DeniedColor;
        }

        var pen = new ImmutablePen(painter.GetBrush(color), theme.WireThickness, new ImmutableDashStyle([4, 3], 0), PenLineCap.Round, PenLineJoin.Round);

        // The preview is a plain elbow from the start anchor to the pointer or to the candidate's anchor
        var from = _start.Anchor;
        var to = _candidate?.Anchor ?? _pointer;
        var midX = (from.X + to.X) / 2;
        var geometry = new StreamGeometry();
        using (var stream = geometry.Open())
        {
            stream.BeginFigure(from.ToAvalonia(), false);
            stream.LineTo(new AvaloniaPoint(midX, from.Y));
            stream.LineTo(new AvaloniaPoint(midX, to.Y));
            stream.LineTo(to.ToAvalonia());
            stream.EndFigure(false);
        }

        context.DrawGeometry(null, pen, geometry);

        // Ring the drop target and explain a rejection right where the user is looking
        if (_candidate is null)
        {
            return;
        }

        context.DrawEllipse(null, painter.GetPen(color, 2), to.ToAvalonia(), 9, 9);
        if (!_check.IsAllowed && _check.Reason is { Length: > 0 } reason)
        {
            var style = new TextStyle(theme.FontFamily, 11 / zoom, DeniedColor, true);
            context.DrawText(TextFormats.Create(reason, style), new AvaloniaPoint(_pointer.X + (12 / zoom), _pointer.Y + (12 / zoom)));
        }
    }
}
