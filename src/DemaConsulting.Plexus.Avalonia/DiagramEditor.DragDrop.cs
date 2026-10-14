using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using DemaConsulting.Plexus.Scene;
using AvaloniaPoint = Avalonia.Point;
using PlexusPage = DemaConsulting.Plexus.DiagramPage;
using PlexusPoint = DemaConsulting.Plexus.Geometry.Point;

namespace DemaConsulting.Plexus.Avalonia;

/// <content>
///     Drag-and-drop node creation: dropping a node type key (for example from a host palette) adds a node,
///     and a translucent ghost of the node shows where it will land while the drag is over the editor.
/// </content>
public sealed partial class DiagramEditor
{
    /// <summary>
    ///     The format identifier of <see cref="NodeTypeDataFormat"/>.
    /// </summary>
    private const string NodeTypeFormatId = "demaconsulting.plexus.node-type";

    /// <summary>
    ///     The opacity of the drop ghost, low enough to see the canvas through it and high enough to read it.
    /// </summary>
    private const double GhostOpacity = 0.55;

    /// <summary>
    ///     The node prototypes the ghost is drawn from, built once per type.
    /// </summary>
    private readonly NodePrototypeCache _prototypes = new();

    /// <summary>
    ///     The ghost currently shown, or null.
    /// </summary>
    private NodeGhost? _ghost;

    /// <summary>
    ///     A node prototype and the diagram position where it is drawn as a ghost.
    /// </summary>
    /// <param name="Prototype">The prototype to draw.</param>
    /// <param name="Position">The top-left corner of the node box in diagram units.</param>
    private readonly record struct NodeGhost(NodePrototype Prototype, PlexusPoint Position);

    /// <summary>
    ///     Gets the data format of a drag payload that carries a node type key (a string, as registered in <see cref="Diagram.Types"/>).
    /// </summary>
    /// <remarks>
    ///     Hosts normally do not use this directly: <see cref="StartNodeTypeDragAsync"/> starts a
    ///     drag with this payload in one call. It is public so hosts can add the format to a larger
    ///     <see cref="DataTransfer"/> of their own (see <see cref="CreateNodeTypeDataTransfer"/>), or
    ///     accept the same payload in other controls.
    /// </remarks>
    public static DataFormat<string> NodeTypeDataFormat { get; } = DataFormat.CreateStringApplicationFormat(NodeTypeFormatId);

    /// <summary>
    ///     Starts a drag of a node type that the editor accepts as a drop, creating a node where it is released.
    /// </summary>
    /// <remarks>
    ///     Call it from the <c>PointerPressed</c> handler of a palette entry (Avalonia needs the press
    ///     event to start a drag): <c>_ = DiagramEditor.StartNodeTypeDragAsync(e, nodeType.Key);</c>.
    ///     Dropping onto an editor adds a node of that type centered at the drop point (snapped to the
    ///     grid when <see cref="SnapToGrid"/> is on) as one undo entry and selects it. Drops of unknown
    ///     type keys, and drops onto a read-only editor, are ignored.
    /// </remarks>
    /// <param name="e">The pointer press that starts the drag.</param>
    /// <param name="typeKey">The key of the node type to create.</param>
    /// <returns>A task producing the effect the drop target applied: <see cref="DragDropEffects.Copy"/> when a node was created, otherwise <see cref="DragDropEffects.None"/>.</returns>
    /// <exception cref="ArgumentNullException">Thrown when an argument is null.</exception>
    public static async Task<DragDropEffects> StartNodeTypeDragAsync(PointerPressedEventArgs e, string typeKey)
    {
        ArgumentNullException.ThrowIfNull(e);
        ArgumentNullException.ThrowIfNull(typeKey);
        using var transfer = CreateNodeTypeDataTransfer(typeKey);
        return await DragDrop.DoDragDropAsync(e, transfer, DragDropEffects.Copy).ConfigureAwait(true);
    }

    /// <summary>
    ///     Makes a control (typically a palette entry) a drag source for a node type, in one call.
    /// </summary>
    /// <remarks>
    ///     Pressing the control and dragging a few pixels starts the same drag as
    ///     <see cref="StartNodeTypeDragAsync"/>, while clicks and double-clicks on the control keep
    ///     working. Dispose the result to stop listening.
    /// </remarks>
    /// <example>
    ///     Make each button of a host-built toolbox draggable onto the editor (the editor itself is the drop target and needs no setup).
    ///     <code><![CDATA[
    ///     using Avalonia.Controls;
    ///     using DemaConsulting.Plexus;
    ///     using DemaConsulting.Plexus.Avalonia;
    ///
    ///     static class ToolboxSample
    ///     {
    ///         public static StackPanel Build(NodeTypeRegistry types)
    ///         {
    ///             var toolbox = new StackPanel();
    ///             foreach (var type in types)
    ///             {
    ///                 var button = new Button { Content = type.DisplayName };    // any control can be a drag source
    ///                 DiagramEditor.EnableNodeTypeDrag(button, type.Key);        // press and drag a few pixels to start the drag
    ///                 toolbox.Children.Add(button);
    ///             }
    ///
    ///             return toolbox;
    ///         }
    ///     }
    ///     ]]></code>
    /// </example>
    /// <param name="source">The control to drag from.</param>
    /// <param name="typeKey">The key of the node type to create on drop.</param>
    /// <returns>A handle that removes the drag behavior when disposed (it can also simply be dropped when the control is discarded).</returns>
    /// <exception cref="ArgumentNullException">Thrown when an argument is null.</exception>
    public static IDisposable EnableNodeTypeDrag(Control source, string typeKey)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(typeKey);
        return new NodeTypeDragSource(source, typeKey);
    }

    /// <summary>
    ///     Creates the drag payload that carries a node type key, for hosts that start the drag themselves.
    /// </summary>
    /// <param name="typeKey">The key of the node type to create on drop.</param>
    /// <returns>The payload; dispose it when the drag is over.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="typeKey"/> is null.</exception>
    public static DataTransfer CreateNodeTypeDataTransfer(string typeKey)
    {
        ArgumentNullException.ThrowIfNull(typeKey);
        var transfer = new DataTransfer();
        transfer.Add(DataTransferItem.Create(NodeTypeDataFormat, typeKey));
        return transfer;
    }

    /// <summary>
    ///     Turns the editor into a drop target for node type payloads.
    /// </summary>
    private void InitializeNodeDrop()
    {
        DragDrop.SetAllowDrop(this, true);
        AddHandler(DragDrop.DragEnterEvent, OnNodeDragOver);
        AddHandler(DragDrop.DragOverEvent, OnNodeDragOver);
        AddHandler(DragDrop.DragLeaveEvent, OnNodeDragLeave);
        AddHandler(DragDrop.DropEvent, OnNodeDrop);
    }

    /// <summary>
    ///     Gets the type of the node whose translucent ghost is shown while a node-type drag is over the editor, or null.
    /// </summary>
    internal NodeType? GhostNodeType => _ghost?.Prototype.Type;

    /// <summary>
    ///     Gets the top-left corner of the ghost, which is where a drop at the same moment creates the node, or null.
    /// </summary>
    internal PlexusPoint? GhostPosition => _ghost?.Position;

    /// <summary>
    ///     Tells the drag source whether a drop here would create a node, and shows the ghost where it would land.
    /// </summary>
    /// <param name="sender">The editor.</param>
    /// <param name="e">The drag event.</param>
    private void OnNodeDragOver(object? sender, DragEventArgs e)
    {
        // Only our payload is answered; foreign drags are left to other handlers
        if (!e.DataTransfer.Contains(NodeTypeDataFormat))
        {
            return;
        }

        e.Handled = true;
        if (TryGetDroppedType(e.DataTransfer, out var type) && type is not null && LivePage() is { } page)
        {
            e.DragEffects = DragDropEffects.Copy;
            ShowGhost(type, page, e.GetPosition(this));
        }
        else
        {
            e.DragEffects = DragDropEffects.None;
            ClearGhost();
        }
    }

    /// <summary>
    ///     Removes the ghost when the drag leaves the editor or is canceled.
    /// </summary>
    /// <param name="sender">The editor.</param>
    /// <param name="e">The drag event.</param>
    private void OnNodeDragLeave(object? sender, DragEventArgs e) => ClearGhost();

    /// <summary>
    ///     Creates the dropped node type at the drop point.
    /// </summary>
    /// <param name="sender">The editor.</param>
    /// <param name="e">The drop event.</param>
    private void OnNodeDrop(object? sender, DragEventArgs e)
    {
        if (!e.DataTransfer.Contains(NodeTypeDataFormat))
        {
            return;
        }

        e.Handled = true;
        e.DragEffects = DragDropEffects.None;
        ClearGhost();
        if (LivePage() is not { } page || !TryGetDroppedType(e.DataTransfer, out var type) || type is null)
        {
            return;
        }

        // The node lands exactly where the ghost was shown: both use the same position computation
        var node = page.AddNode(type, DropPosition(type, page, e.GetPosition(this)));
        SetSelection([node]);
        Focus();
        e.DragEffects = DragDropEffects.Copy;
    }

    /// <summary>
    ///     Computes where a node dropped at a point is placed: centered on the point, then snapped like any other placement.
    /// </summary>
    /// <param name="type">The dropped type.</param>
    /// <param name="page">The page that receives the node.</param>
    /// <param name="screen">The drop point relative to the editor.</param>
    /// <returns>The top-left corner of the new node in diagram coordinates.</returns>
    private PlexusPoint DropPosition(NodeType type, PlexusPage page, AvaloniaPoint screen)
    {
        // Center the node on the point, then snap its top-left corner
        var at = ToDiagram(screen);
        var size = NodeGeometry.DefaultNodeSize(type, page.Diagram.Theme);
        return new PlexusPoint(Snap(at.X - (size.Width / 2)), Snap(at.Y - (size.Height / 2)));
    }

    /// <summary>
    ///     Shows (or moves) the ghost of a node type at the position a drop at a point would use.
    /// </summary>
    /// <param name="type">The dragged type.</param>
    /// <param name="page">The page that would receive the node.</param>
    /// <param name="screen">The pointer position relative to the editor.</param>
    private void ShowGhost(NodeType type, PlexusPage page, AvaloniaPoint screen)
    {
        var position = DropPosition(type, page, screen);
        var prototype = _prototypes.Get(page.Diagram.Types, type, page.Diagram.Theme, EffectiveTheme, _measurer);

        // Repaint only when the ghost actually moved to a new grid cell or changed type
        if (_ghost is { } current && ReferenceEquals(current.Prototype, prototype) && current.Position == position)
        {
            return;
        }

        _ghost = new NodeGhost(prototype, position);
        InvalidateVisual();
    }

    /// <summary>
    ///     Removes the ghost, if shown.
    /// </summary>
    private void ClearGhost()
    {
        if (_ghost is not null)
        {
            _ghost = null;
            InvalidateVisual();
        }
    }

    /// <summary>
    ///     Paints the ghost translucently; the context must carry the diagram-to-screen transform.
    /// </summary>
    /// <param name="context">The drawing context.</param>
    private void PaintGhost(DrawingContext context)
    {
        if (_ghost is not { } ghost)
        {
            return;
        }

        // Same painter and prototype as the palette, so the ghost is the node as it will look, just see-through
        using (context.PushOpacity(GhostOpacity))
        using (context.PushTransform(Matrix.CreateTranslation(ghost.Position.X, ghost.Position.Y)))
        {
            ghost.Prototype.Paint(context, _painter, Zoom);
        }
    }

    /// <summary>
    ///     Reads the node type a drop carries, when the editor can create it.
    /// </summary>
    /// <param name="data">The drag payload.</param>
    /// <param name="type">Receives the registered node type.</param>
    /// <returns><see langword="true"/> when the editor is editable, shows a page and the payload names a registered type.</returns>
    private bool TryGetDroppedType(IDataTransfer data, out NodeType? type)
    {
        type = null;
        return !IsReadOnly
            && LivePage() is { } page
            && data.TryGetValue(NodeTypeDataFormat) is { } key
            && page.Diagram.Types.TryGet(key, out type);
    }
}
