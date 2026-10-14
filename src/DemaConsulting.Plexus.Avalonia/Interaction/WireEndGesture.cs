using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using DemaConsulting.Plexus.Scene;
using AvaloniaPoint = Avalonia.Point;
using PlexusColor = DemaConsulting.Plexus.Geometry.Color;
using PlexusPoint = DemaConsulting.Plexus.Geometry.Point;

namespace DemaConsulting.Plexus.Avalonia.Interaction;

/// <summary>
///     Drags one end of a selected wire to another port and reconnects the wire there.
/// </summary>
/// <remarks>
///     While dragging, a preview wire from the end that stays put follows the pointer and the model
///     is not touched. When the pointer is over another port the move is checked with
///     <see cref="DiagramPage.CanReconnect"/> (which, unlike <see cref="DiagramPage.CanConnect"/>, does not count
///     the wire itself as a duplicate), and the preview turns green (allowed) or red with the reason
///     beside the pointer (rejected). Releasing over an allowed port calls <see cref="Wire.Reconnect"/>,
///     which re-routes the wire and keeps its id as one undo step; releasing anywhere else cancels and
///     the wire stays as it was. Only an output can take the source end and only an input the target end.
/// </remarks>
internal sealed class WireEndGesture : EditorGesture
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
    ///     The wire whose end is dragged.
    /// </summary>
    private readonly Wire _wire;

    /// <summary>
    ///     Whether the source end (true) or the target end (false) is dragged.
    /// </summary>
    private readonly bool _dragSource;

    /// <summary>
    ///     The port currently under the pointer, if it is a different port than the one the end is on.
    /// </summary>
    private Port? _candidate;

    /// <summary>
    ///     The verdict for reconnecting to <see cref="_candidate"/>.
    /// </summary>
    private ConnectionCheck _check;

    /// <summary>
    ///     The pointer position in diagram coordinates.
    /// </summary>
    private PlexusPoint _pointer;

    /// <summary>
    ///     Whether the pointer has moved far enough to count as a drag.
    /// </summary>
    private bool _dragging;

    /// <summary>
    ///     Initializes a new instance of the <see cref="WireEndGesture"/> class.
    /// </summary>
    /// <param name="editor">The editor.</param>
    /// <param name="startScreen">The control-relative position of the press.</param>
    /// <param name="wire">The wire whose end is dragged.</param>
    /// <param name="dragSource">Whether the source end (true) or the target end (false) is dragged.</param>
    public WireEndGesture(DiagramEditor editor, AvaloniaPoint startScreen, Wire wire, bool dragSource)
        : base(editor, startScreen)
    {
        _wire = wire;
        _dragSource = dragSource;
        _pointer = editor.ToDiagram(startScreen);
    }

    /// <inheritdoc />
    public override StandardCursorType? Cursor => StandardCursorType.Cross;

    /// <inheritdoc />
    public override void Move(AvaloniaPoint screen, KeyModifiers modifiers)
    {
        _dragging |= DistanceFromStart(screen) >= DragThreshold;
        if (!_dragging)
        {
            return;
        }

        // Find a port under the pointer other than the one the end is on and ask the model about it
        _pointer = Editor.ToDiagram(screen);
        var current = _dragSource ? _wire.Source : _wire.Target;
        var hit = Editor.HitTestDiagram(_pointer);
        _candidate = hit.Port is { } port && !ReferenceEquals(port, current) ? port : null;
        _check = default;
        if (_candidate is not null && Editor.LivePage() is { } page)
        {
            _check = _dragSource ? page.CanReconnect(_wire, _candidate, null) : page.CanReconnect(_wire, null, _candidate);
        }

        Editor.InvalidateVisual();
    }

    /// <inheritdoc />
    public override void Complete(AvaloniaPoint screen, KeyModifiers modifiers)
    {
        // Re-evaluate at the release position, which may differ from the last move event
        Move(screen, modifiers);
        if (_dragging && _candidate is not null && _check.IsAllowed && Editor.LivePage() is not null)
        {
            try
            {
                if (_dragSource)
                {
                    _wire.Reconnect(_candidate, null);
                }
                else
                {
                    _wire.Reconnect(null, _candidate);
                }
            }
            catch (InvalidOperationException)
            {
                // The model re-checks the rules when it reconnects; losing a race to a change of
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
        if (!_dragging)
        {
            return;
        }

        // Neutral while searching, green or red once there is a verdict
        var color = theme.SelectionColor;
        if (_candidate is not null)
        {
            color = _check.IsAllowed ? AllowedColor : DeniedColor;
        }

        var pen = new ImmutablePen(painter.GetBrush(color), theme.WireThickness, new ImmutableDashStyle([4, 3], 0), PenLineCap.Round, PenLineJoin.Round);

        // The preview is a plain elbow from the end that stays put to the pointer or to the candidate's anchor
        var dragged = _candidate?.Anchor ?? _pointer;
        var from = _dragSource ? dragged : _wire.Source.Anchor;
        var to = _dragSource ? _wire.Target.Anchor : dragged;
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

        context.DrawEllipse(null, painter.GetPen(color, 2), dragged.ToAvalonia(), 9, 9);
        if (!_check.IsAllowed && _check.Reason is { Length: > 0 } reason)
        {
            var style = new TextStyle(theme.FontFamily, 11 / zoom, DeniedColor, true);
            context.DrawText(TextFormats.Create(reason, style), new AvaloniaPoint(_pointer.X + (12 / zoom), _pointer.Y + (12 / zoom)));
        }
    }
}
