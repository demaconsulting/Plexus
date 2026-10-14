namespace DemaConsulting.Plexus.Avalonia.PropertyGrid.Diagrams;

/// <summary>
///     Where the live preview of a node properties dialog is placed relative to the grid (see
///     <see cref="NodePropertiesDialogOptions.PreviewPlacement"/>).
/// </summary>
public enum NodePreviewPlacement
{
    /// <summary>
    ///     The preview is to the right of the grid, side by side; the default dialog is widened to fit it.
    /// </summary>
    Side,

    /// <summary>
    ///     The preview is above the grid; the default dialog is made taller to fit it.
    /// </summary>
    Top,
}
