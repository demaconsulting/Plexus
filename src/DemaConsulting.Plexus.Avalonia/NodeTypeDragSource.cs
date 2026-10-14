using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace DemaConsulting.Plexus.Avalonia;

/// <summary>
///     Turns any control into a drag source for one node type: pressing and dragging it starts a node-type drag.
/// </summary>
/// <remarks>
///     The drag starts only after the pointer has moved a few pixels with the left button down, so
///     plain clicks and double-clicks on the control keep working. Created through
///     <see cref="DiagramEditor.EnableNodeTypeDrag"/>.
/// </remarks>
internal sealed class NodeTypeDragSource : IDisposable
{
    /// <summary>
    ///     The distance, in device-independent pixels, the pointer must travel before a drag starts.
    /// </summary>
    private const double DragThreshold = 4;

    /// <summary>
    ///     The control that acts as the drag source.
    /// </summary>
    private readonly Control _source;

    /// <summary>
    ///     The key of the node type that is dragged.
    /// </summary>
    private readonly string _typeKey;

    /// <summary>
    ///     Starts the drag once the threshold is passed; the editor's own starter unless a host or test supplies another.
    /// </summary>
    private readonly Func<PointerPressedEventArgs, string, Task> _starter;

    /// <summary>
    ///     The left-button press that may become a drag, or null.
    /// </summary>
    private PointerPressedEventArgs? _press;

    /// <summary>
    ///     Where the pending press happened, relative to the source.
    /// </summary>
    private Point _pressPosition;

    /// <summary>
    ///     Initializes a new instance of the <see cref="NodeTypeDragSource"/> class and starts listening.
    /// </summary>
    /// <param name="source">The control to drag from.</param>
    /// <param name="typeKey">The node type key.</param>
    public NodeTypeDragSource(Control source, string typeKey)
        : this(source, typeKey, async (press, key) => await DiagramEditor.StartNodeTypeDragAsync(press, key).ConfigureAwait(true))
    {
    }

    /// <summary>
    ///     Initializes a new instance of the <see cref="NodeTypeDragSource"/> class with a custom drag starter.
    /// </summary>
    /// <remarks>
    ///     The headless test platform cannot run an operating-system drag, so tests (and the
    ///     <see cref="NodePalette"/>, which forwards its own starter) substitute the function that
    ///     is called when the threshold is passed.
    /// </remarks>
    /// <param name="source">The control to drag from.</param>
    /// <param name="typeKey">The node type key.</param>
    /// <param name="starter">The function that starts the drag from the press and the node type key.</param>
    public NodeTypeDragSource(Control source, string typeKey, Func<PointerPressedEventArgs, string, Task> starter)
    {
        _source = source;
        _typeKey = typeKey;
        _starter = starter;

        // Handled events too: list items and buttons mark their presses as handled
        source.AddHandler(InputElement.PointerPressedEvent, OnPressed, RoutingStrategies.Bubble | RoutingStrategies.Tunnel, handledEventsToo: true);
        source.AddHandler(InputElement.PointerMovedEvent, OnMoved, RoutingStrategies.Bubble | RoutingStrategies.Tunnel, handledEventsToo: true);
        source.AddHandler(InputElement.PointerReleasedEvent, OnReleased, RoutingStrategies.Bubble | RoutingStrategies.Tunnel, handledEventsToo: true);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _source.RemoveHandler(InputElement.PointerPressedEvent, OnPressed);
        _source.RemoveHandler(InputElement.PointerMovedEvent, OnMoved);
        _source.RemoveHandler(InputElement.PointerReleasedEvent, OnReleased);
        _press = null;
    }

    /// <summary>
    ///     Remembers a left press as a potential drag start.
    /// </summary>
    /// <param name="sender">The source control.</param>
    /// <param name="e">The press.</param>
    private void OnPressed(object? sender, PointerPressedEventArgs e)
    {
        var point = e.GetCurrentPoint(_source);
        if (point.Properties.IsLeftButtonPressed)
        {
            _press = e;
            _pressPosition = point.Position;
        }
    }

    /// <summary>
    ///     Starts the drag once the pointer has moved far enough.
    /// </summary>
    /// <param name="sender">The source control.</param>
    /// <param name="e">The move.</param>
    private void OnMoved(object? sender, PointerEventArgs e)
    {
        if (_press is not { } press)
        {
            return;
        }

        var point = e.GetCurrentPoint(_source);
        if (!point.Properties.IsLeftButtonPressed)
        {
            _press = null;
            return;
        }

        if (Math.Abs(point.Position.X - _pressPosition.X) >= DragThreshold || Math.Abs(point.Position.Y - _pressPosition.Y) >= DragThreshold)
        {
            _press = null;
            _ = StartAsync(press);
        }
    }

    /// <summary>
    ///     Forgets the press when the button comes up before the threshold was reached (a plain click).
    /// </summary>
    /// <param name="sender">The source control.</param>
    /// <param name="e">The release.</param>
    private void OnReleased(object? sender, PointerReleasedEventArgs e) => _press = null;

    /// <summary>
    ///     Runs the drag and swallows platform failures, since nobody awaits it.
    /// </summary>
    /// <param name="press">The press that starts the drag.</param>
    /// <returns>A task that always completes successfully.</returns>
    private async Task StartAsync(PointerPressedEventArgs press)
    {
        try
        {
            await _starter(press, _typeKey).ConfigureAwait(true);
        }
        catch (Exception ex) when (ex is InvalidOperationException or NotSupportedException or System.Runtime.InteropServices.ExternalException)
        {
            // Deliberately ignored: a platform that cannot drag simply leaves double-click as the way to add nodes
        }
    }
}
