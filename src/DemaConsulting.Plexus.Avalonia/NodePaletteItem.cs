using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using DemaConsulting.Plexus.Scene;
using AvaloniaPoint = Avalonia.Point;
using AvaloniaRect = Avalonia.Rect;
using AvaloniaSize = Avalonia.Size;

namespace DemaConsulting.Plexus.Avalonia;

/// <summary>
///     One entry of a <see cref="NodePalette"/>: draws the prototype node of a type exactly as the canvas draws it.
/// </summary>
/// <remarks>
///     The item paints through the palette's shared <see cref="ScenePainter"/> with the prototype's
///     scene, scaled by the palette's zoom. All items of a palette share one box column: the item is
///     as wide as the widest visual extent of any prototype, measured from the node box's center
///     (<see cref="NodePrototype.HorizontalReach"/>) in both directions, plus padding, and the palette's
///     panel arranges it in a grid cell at least that wide. Its node box is centered horizontally in the cell, rounded to
///     a whole device-independent pixel, so the box centers of all items coincide (within half a pixel
///     when box widths differ in parity) whatever the port labels around them. The height comes from
///     <see cref="NodePrototype.VisualBounds"/> (name label, box, ports, port labels and summary) plus
///     padding, and the box starts on a whole device-independent pixel row, the same row for every item
///     of the palette so the boxes of a row line up, which
///     keeps its edges crisp and makes the pixels equal to a node placed on a canvas at the same zoom.
///     Presses are forwarded to the palette, which selects, activates and (through a
///     <see cref="NodeTypeDragSource"/>) starts drags.
/// </remarks>
internal sealed class NodePaletteItem : Control
{
    /// <summary>
    ///     The empty space, in device-independent pixels, kept around the node's visual extent.
    /// </summary>
    internal const double Padding = 8;

    /// <summary>
    ///     The palette the item belongs to.
    /// </summary>
    private readonly NodePalette _owner;

    /// <summary>
    ///     The painter shared with the palette.
    /// </summary>
    private readonly ScenePainter _painter;

    /// <summary>
    ///     The theme used for the selection and hover outlines.
    /// </summary>
    private readonly DiagramTheme _theme;

    /// <summary>
    ///     The zoom factor the item is drawn and sized at.
    /// </summary>
    private readonly double _zoom;

    /// <summary>
    ///     The node box's width in device-independent pixels at the item's zoom.
    /// </summary>
    private readonly double _boxWidth;

    /// <summary>
    ///     The vertical distance, in device-independent pixels, from the item's top edge to the node box's top edge.
    /// </summary>
    private readonly double _originY;

    /// <summary>
    ///     The item's minimum size in device-independent pixels; the width may be larger when the palette is wider.
    /// </summary>
    private readonly AvaloniaSize _size;

    /// <summary>
    ///     Whether the item is the palette's selected item.
    /// </summary>
    private bool _isSelected;

    /// <summary>
    ///     Initializes a new instance of the <see cref="NodePaletteItem"/> class.
    /// </summary>
    /// <param name="owner">The palette the item belongs to.</param>
    /// <param name="prototype">The prototype to draw.</param>
    /// <param name="painter">The painter, shared with the palette.</param>
    /// <param name="theme">The theme the prototype was built with.</param>
    /// <param name="zoom">The zoom factor to draw at.</param>
    /// <param name="columnReach">The palette-wide horizontal reach, in diagram units, from a box's center to the edge of the widest visual extent.</param>
    /// <param name="topReach">The palette-wide vertical reach, in diagram units, from a box's top edge up to the top of the highest visual extent, so every box top lines up.</param>
    public NodePaletteItem(NodePalette owner, NodePrototype prototype, ScenePainter painter, DiagramTheme theme, double zoom, double columnReach, double topReach)
    {
        _owner = owner;
        Prototype = prototype;
        _painter = painter;
        _theme = theme;
        _zoom = zoom;
        _boxWidth = prototype.Box.Width * zoom;

        // The box column is as wide as the widest extent of any item, symmetric about the box center;
        // vertically the node box sits at the diagram origin, so the extent's negative top offset says how
        // far the name label reaches above it, and the highest of them sets the common box top, so the boxes
        // of a row line up. Whole pixels keep the box edges crisp.
        var bounds = prototype.VisualBounds;
        _originY = Padding + Math.Ceiling(Math.Max(topReach, -bounds.Top) * zoom);
        _size = new AvaloniaSize(
            2 * (Padding + Math.Ceiling(Math.Max(columnReach, prototype.HorizontalReach) * zoom)),
            _originY + Math.Ceiling(bounds.Bottom * zoom) + Padding);

        // Accessible name and a stable id so UI automation can find each type
        AutomationProperties.SetName(this, prototype.Type.DisplayName);
        AutomationProperties.SetAutomationId(this, NodePalette.GetAutomationId(prototype.Type));
        Cursor = new Cursor(StandardCursorType.Hand);
        Drag = new NodeTypeDragSource(this, prototype.Type.Key, owner.StartDragAsync);
    }

    /// <summary>
    ///     Gets the prototype the item draws.
    /// </summary>
    public NodePrototype Prototype { get; }

    /// <summary>
    ///     Gets the node type the item represents.
    /// </summary>
    public NodeType NodeType => Prototype.Type;

    /// <summary>
    ///     Gets the drag behavior attached to the item.
    /// </summary>
    internal NodeTypeDragSource Drag { get; }

    /// <summary>
    ///     Gets the position of the node box's top-left corner relative to the item.
    /// </summary>
    internal AvaloniaPoint BoxOrigin => new(OriginX, _originY);

    /// <summary>
    ///     Gets the horizontal distance, in device-independent pixels, from the item's left edge to the node box's left edge.
    /// </summary>
    /// <remarks>
    ///     The box is centered in the item's current width (its minimum width before the first arrange).
    /// </remarks>
    private double OriginX => Math.Round((Math.Max(Bounds.Width, _size.Width) - _boxWidth) / 2);

    /// <summary>
    ///     Gets or sets a value indicating whether the item is drawn as the selected one.
    /// </summary>
    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (_isSelected != value)
            {
                _isSelected = value;
                InvalidateVisual();
            }
        }
    }

    /// <summary>
    ///     Selects and activates the item, which is what UI automation's Invoke pattern does.
    /// </summary>
    internal void Invoke()
    {
        _owner.Select(NodeType);
        _owner.Activate(NodeType);
    }

    /// <inheritdoc />
    public override void Render(DrawingContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        // A transparent fill makes the whole item hit-testable, not just the painted pixels
        var area = new AvaloniaRect(0, 0, Math.Max(Bounds.Width, _size.Width), Math.Max(Bounds.Height, _size.Height));
        context.FillRectangle(Brushes.Transparent, area);
        if (_isSelected)
        {
            context.DrawRectangle(null, _painter.GetPen(_theme.SelectionColor, _theme.SelectionThickness), area.Deflate(_theme.SelectionThickness / 2), 3, 3);
        }
        else if (IsPointerOver)
        {
            context.DrawRectangle(null, _painter.GetPen(_theme.HoverColor, _theme.SelectionThickness), area.Deflate(_theme.SelectionThickness / 2), 3, 3);
        }

        // Same painter and scene as the canvas; only the transform differs
        using (context.PushTransform(Matrix.CreateScale(_zoom, _zoom) * Matrix.CreateTranslation(OriginX, _originY)))
        {
            Prototype.Paint(context, _painter, _zoom);
        }
    }

    /// <inheritdoc />
    protected override void OnPointerEntered(PointerEventArgs e)
    {
        base.OnPointerEntered(e);
        InvalidateVisual();
    }

    /// <inheritdoc />
    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        InvalidateVisual();
    }

    /// <inheritdoc />
    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        ArgumentNullException.ThrowIfNull(e);

        // A left press selects; the second press of a double-click activates
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            _owner.OnItemPressed(this, e.ClickCount);
        }
    }

    /// <inheritdoc />
    protected override AvaloniaSize MeasureOverride(AvaloniaSize availableSize) => _size;

    /// <inheritdoc />
    protected override AutomationPeer OnCreateAutomationPeer() => new NodePaletteItemAutomationPeer(this);
}
