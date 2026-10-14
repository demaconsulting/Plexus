using Avalonia.Controls;

namespace DemaConsulting.Plexus.Avalonia.PropertyGrid.Diagrams;

/// <summary>
///     The optional settings of a node properties dialog (see <see cref="NodePropertiesDialog.ShowAsync(Window, Node, NodePropertiesDialogOptions?)"/>):
///     a live preview panel next to the grid, the title and the size. Every member is optional; unset members give the
///     plain dialog.
/// </summary>
/// <remarks>
///     <para>
///         The preview lets a host build a custom node inspector: a visualization of what the settings do (a waveform, a
///         curve, a response plot) that redraws while the user edits and before OK. <see cref="Preview"/> is called once
///         when the dialog is created with a <see cref="NodePreviewContext"/> that reads the effective (staged) values and
///         raises <see cref="NodePreviewContext.Changed"/> whenever they change. The preview never writes to the node: OK
///         still commits everything as one undo entry and Cancel still changes nothing.
///     </para>
///     <para>
///         The record is immutable and can be shared between dialogs; the factory is called for each dialog.
///     </para>
/// </remarks>
/// <example>
///     <code><![CDATA[
///     using Avalonia.Controls;
///     using DemaConsulting.Plexus.Avalonia.PropertyGrid.Diagrams;
///
///     var options = new NodePropertiesDialogOptions
///     {
///         Preview = context =>
///         {
///             var text = new TextBlock();
///             void Update() => text.Text = $"{context.GetPropertyValue("Frequency")} Hz";   // the staged value
///             context.Changed += (_, _) => Update();                                       // redraw on every edit, reset and cancel
///             Update();
///             return text;
///         },
///         PreviewPlacement = NodePreviewPlacement.Side,   // the default: to the right of the grid
///     };
///     ]]></code>
/// </example>
public sealed record NodePropertiesDialogOptions
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="NodePropertiesDialogOptions"/> record with every member unset (the plain dialog).
    /// </summary>
    public NodePropertiesDialogOptions()
    {
        // Nothing to initialize: every member has a usable default
    }

    /// <summary>
    ///     Gets the factory of the preview control, or <see langword="null"/> for no preview (the plain dialog).
    /// </summary>
    /// <remarks>
    ///     It is called once, on the UI thread, while the dialog is created. It receives the context of the dialog's grid and
    ///     must return a control (never <see langword="null"/>); subscribe to <see cref="NodePreviewContext.Changed"/> to
    ///     redraw. The context is disposed when the dialog closes, so no notification arrives after that. The control is
    ///     shown in a panel with the automation id <c>PropertyGridDialog_Preview</c>.
    /// </remarks>
    public Func<NodePreviewContext, Control>? Preview { get; init; }

    /// <summary>
    ///     Gets where the preview is placed; the default <see cref="NodePreviewPlacement.Side"/> puts it to the right of
    ///     the grid. Ignored without a <see cref="Preview"/>.
    /// </summary>
    public NodePreviewPlacement PreviewPlacement { get; init; } = NodePreviewPlacement.Side;

    /// <summary>
    ///     Gets the window title; <see langword="null"/> uses <see cref="NodePropertiesDialog.GetTitle"/>
    ///     (<c>"{Name} - Properties"</c>).
    /// </summary>
    public string? Title { get; init; }

    /// <summary>
    ///     Gets the initial width of the dialog in device-independent pixels; it must be a positive finite number.
    ///     <see langword="null"/> means 480, or 880 with a side preview.
    /// </summary>
    public double? Width { get; init; }

    /// <summary>
    ///     Gets the initial height of the dialog in device-independent pixels; it must be a positive finite number.
    ///     <see langword="null"/> means 640, or 860 with a top preview.
    /// </summary>
    public double? Height { get; init; }
}
