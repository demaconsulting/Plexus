using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace DemaConsulting.Plexus.Avalonia.PropertyGrid;

/// <summary>
///     The theme resources and fixed metrics the property grid uses. Brushes come from the Fluent theme resources
///     (so the grid follows light and dark themes and theme changes at run time) with a neutral fallback for hosts
///     that do not load a theme that defines them.
/// </summary>
internal static class GridTheme
{
    /// <summary>
    ///     The height of a property row in device-independent pixels.
    /// </summary>
    internal const double RowHeight = 22;

    /// <summary>
    ///     The font size of the grid's text.
    /// </summary>
    internal const double FontSize = 12;

    /// <summary>
    ///     The horizontal space reserved for the expander of a row.
    /// </summary>
    internal const double ExpanderWidth = 16;

    /// <summary>
    ///     The extra indentation per nesting level.
    /// </summary>
    internal const double IndentWidth = 14;

    /// <summary>
    ///     The resource key of the primary text brush.
    /// </summary>
    internal const string TextKey = "SystemControlForegroundBaseHighBrush";

    /// <summary>
    ///     The resource key of the secondary (dimmed) text brush.
    /// </summary>
    internal const string SecondaryTextKey = "SystemControlForegroundBaseMediumBrush";

    /// <summary>
    ///     The resource key of the grid line brush.
    /// </summary>
    internal const string LineKey = "SystemControlForegroundBaseLowBrush";

    /// <summary>
    ///     The resource key of the content background brush.
    /// </summary>
    internal const string BackgroundKey = "SystemControlBackgroundAltHighBrush";

    /// <summary>
    ///     The resource key of the brush of bands and toolbars.
    /// </summary>
    internal const string BandKey = "SystemControlBackgroundChromeMediumLowBrush";

    /// <summary>
    ///     The resource key of the hover brush.
    /// </summary>
    internal const string HoverKey = "SystemControlHighlightListLowBrush";

    /// <summary>
    ///     The resource key of the selection brush.
    /// </summary>
    internal const string SelectionKey = "SystemControlHighlightAccentBrush";

    /// <summary>
    ///     The brush of the validation error border (the same red works on light and dark backgrounds).
    /// </summary>
    internal static readonly IBrush ErrorBrush = new SolidColorBrush(Color.Parse("#D13438")).ToImmutable();

    /// <summary>
    ///     The faint tint behind a value cell that has an error.
    /// </summary>
    internal static readonly IBrush ErrorTint = new SolidColorBrush(Color.FromArgb(0x26, 0xD1, 0x34, 0x38)).ToImmutable();

    /// <summary>
    ///     The text color on a selected row.
    /// </summary>
    internal static readonly IBrush SelectedTextBrush = Brushes.White;

    /// <summary>
    ///     The fallback line brush.
    /// </summary>
    private static readonly IBrush FallbackLine = new SolidColorBrush(Color.FromArgb(0x40, 0x80, 0x80, 0x80)).ToImmutable();

    /// <summary>
    ///     The fallback band brush.
    /// </summary>
    private static readonly IBrush FallbackBand = new SolidColorBrush(Color.FromArgb(0x30, 0x80, 0x80, 0x80)).ToImmutable();

    /// <summary>
    ///     The fallback selection brush.
    /// </summary>
    private static readonly IBrush FallbackSelection = new SolidColorBrush(Color.Parse("#0078D7")).ToImmutable();

    /// <summary>
    ///     Binds a brush property of a control to a theme resource, so it follows the theme variant.
    /// </summary>
    /// <param name="target">The control.</param>
    /// <param name="property">The brush property.</param>
    /// <param name="key">The resource key.</param>
    internal static void BindBrush(Control target, AvaloniaProperty<IBrush?> property, string key)
    {
        var fallback = FallbackFor(key);
        target.Bind(property, target.GetResourceObservable(key, value => value as IBrush ?? fallback));
    }

    /// <summary>
    ///     Gets the fallback brush of a resource key.
    /// </summary>
    /// <param name="key">The resource key.</param>
    /// <returns>The brush, or <see langword="null"/> to leave the property at its default.</returns>
    private static IBrush? FallbackFor(string key) => key switch
    {
        LineKey => FallbackLine,
        BandKey => FallbackBand,
        HoverKey => FallbackBand,
        SelectionKey => FallbackSelection,
        BackgroundKey => Brushes.Transparent,
        _ => null,
    };
}
