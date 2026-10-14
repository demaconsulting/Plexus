using Avalonia;

namespace DemaConsulting.Plexus.Avalonia;

/// <summary>
///     Describes a request for a context menu: a right-click on an element or on the empty canvas.
/// </summary>
/// <remarks>
///     The editor selects the element under the pointer (when it was not already selected) before
///     raising the event, so hosts can build a menu from <see cref="DiagramEditor.Selection"/>. The
///     editor opens nothing itself; hosts show their own menu or flyout. Both positions are Avalonia
///     <c>Point</c>s (not the Plexus <c>Point</c>); see the remarks of <see cref="DiagramEditor.DiagramContextRequested"/>.
/// </remarks>
/// <example>
///     Show a menu at the pointer with an "Add node here" entry for the empty canvas and a "Delete" entry for an element.
///     <c>MenuFlyout.ShowAt(editor, true)</c> places the menu at <c>TopLevel.LastPointerPosition</c>, the last pointer position the window saw;
///     as this event is raised on button release, that is the release point.
///     <code><![CDATA[
///     using Avalonia.Controls;
///     using DemaConsulting.Plexus;
///     using DemaConsulting.Plexus.Avalonia;
///
///     static class ContextMenuSample
///     {
///         public static void Wire(DiagramEditor editor, string nodeTypeKey)
///         {
///             editor.DiagramContextRequested += (_, e) =>
///             {
///                 var menu = new MenuFlyout();
///                 if (e.Element is null)
///                 {
///                     var add = new MenuItem { Header = "Add node here" };
///                     add.Click += (_, _) => editor.Page?.AddNode(nodeTypeKey, e.Position.X, e.Position.Y);   // diagram coordinates
///                     menu.Items.Add(add);
///                 }
///                 else
///                 {
///                     var delete = new MenuItem { Header = "Delete" };
///                     delete.Click += (_, _) => editor.Delete(editor.Selection.ToArray());
///                     menu.Items.Add(delete);
///                 }
///
///                 menu.ShowAt(editor, true);                  // true: open at TopLevel.LastPointerPosition (the pointer right now)
///             };
///         }
///     }
///     ]]></code>
///     To open the menu at an exact position instead, use a <c>ContextMenu</c> (it has a placement rectangle, a flyout has not) anchored at <c>e.ScreenPosition</c>, which is relative to the editor:
///     <code><![CDATA[
///     using Avalonia;
///     using Avalonia.Controls;
///     using Avalonia.Controls.Primitives.PopupPositioning;
///     using DemaConsulting.Plexus.Avalonia;
///
///     static class ExplicitContextMenuSample
///     {
///         public static void Wire(DiagramEditor editor)
///         {
///             editor.DiagramContextRequested += (_, e) =>
///             {
///                 var menu = new ContextMenu();
///                 menu.Items.Add(new MenuItem { Header = "Delete", IsEnabled = e.Element is not null });
///
///                 // The menu's top-left corner goes exactly to the click point, whatever the pointer does later
///                 menu.PlacementTarget = editor;
///                 menu.Placement = PlacementMode.AnchorAndGravity;
///                 menu.PlacementAnchor = PopupAnchor.TopLeft;
///                 menu.PlacementGravity = PopupGravity.BottomRight;
///                 menu.PlacementRect = new Rect(e.ScreenPosition, new Size(1, 1));
///                 menu.Open(editor);
///             };
///         }
///     }
///     ]]></code>
/// </example>
public sealed class DiagramContextRequestedEventArgs : EventArgs
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="DiagramContextRequestedEventArgs"/> class.
    /// </summary>
    /// <param name="element">The element under the pointer, or null for the empty canvas.</param>
    /// <param name="position">The pointer position in diagram coordinates.</param>
    /// <param name="screenPosition">The pointer position relative to the editor control.</param>
    public DiagramContextRequestedEventArgs(DiagramElement? element, Point position, Point screenPosition)
    {
        Element = element;
        Position = position;
        ScreenPosition = screenPosition;
    }

    /// <summary>
    ///     Gets the element under the pointer, or null for the empty canvas (the declared type is non-nullable in the generated signature but the value may be null).
    /// </summary>
    public DiagramElement? Element { get; }

    /// <summary>
    ///     Gets the pointer position in diagram coordinates (what <c>DiagramPage.AddNode</c> expects), as an Avalonia <c>Point</c>.
    /// </summary>
    public Point Position { get; }

    /// <summary>
    ///     Gets the pointer position relative to the editor control in device-independent pixels (what a <c>ContextMenu.PlacementRect</c> or an overlay positioned over the editor wants), as an Avalonia <c>Point</c>.
    /// </summary>
    public Point ScreenPosition { get; }
}
