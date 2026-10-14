using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;

namespace DemaConsulting.Plexus.Puppet.Automation;

/// <summary>
///     Walks a menu path such as <c>Page/Move Selection To/Page 2</c> through the real menu bar.
/// </summary>
internal static class MenuNavigator
{
    /// <summary>
    ///     How long a menu gets to open its pop-up before the next item is searched for.
    /// </summary>
    private static readonly TimeSpan OpenDelay = TimeSpan.FromMilliseconds(250);

    /// <summary>
    ///     Splits a menu path into its segments.
    /// </summary>
    /// <param name="path">The path, segments joined with <c>/</c>.</param>
    /// <returns>The non-empty segments.</returns>
    internal static IReadOnlyList<string> Split(string path) =>
        path.Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    /// <summary>
    ///     Builds the AutomationId a path prefix has in the demo (<c>Menu_</c> and the segments joined with underscores).
    /// </summary>
    /// <param name="segments">The path segments.</param>
    /// <param name="count">How many leading segments to use.</param>
    /// <returns>The AutomationId, such as <c>Menu_File_OpenSample</c>.</returns>
    internal static string IdOf(IReadOnlyList<string> segments, int count) =>
        "Menu_" + string.Join('_', segments.Take(count).Select(Compact));

    /// <summary>
    ///     Tests whether a menu item's name is the one a path segment asks for.
    /// </summary>
    /// <remarks>
    ///     Access-key markers (<c>_</c>, <c>&amp;</c>), white space and case are ignored, and a trailing <c>...</c> may be omitted.
    /// </remarks>
    /// <param name="name">The item's name.</param>
    /// <param name="segment">The segment.</param>
    /// <returns><see langword="true"/> when they match.</returns>
    internal static bool NameMatches(string name, string segment)
    {
        var left = Compact(name).TrimEnd('.');
        var right = Compact(segment).TrimEnd('.');
        return left.Length > 0 && string.Equals(left, right, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    ///     Opens every menu on the path and invokes the last item.
    /// </summary>
    /// <param name="context">The attached instance.</param>
    /// <param name="segments">The path segments.</param>
    /// <returns>The outcome: the AutomationId invoked, or the path segment that was missing or disabled.</returns>
    internal static MenuResult Invoke(AttachContext context, IReadOnlyList<string> segments)
    {
        for (var i = 0; i < segments.Count; i++)
        {
            var item = Find(context, segments, i + 1);
            if (item is null)
            {
                return new MenuResult(null, segments[i], false);
            }

            if (!UiActions.Safe(() => item.IsEnabled, true))
            {
                return new MenuResult(UiActions.Describe(item), segments[i], true);
            }

            var last = i == segments.Count - 1;
            Open(item, last);
            if (!last)
            {
                Thread.Sleep(OpenDelay);
            }
        }

        return new MenuResult(IdOf(segments, segments.Count), null, false);
    }

    /// <summary>
    ///     Finds the menu item for the first <paramref name="count"/> segments.
    /// </summary>
    /// <param name="context">The attached instance.</param>
    /// <param name="segments">The path segments.</param>
    /// <param name="count">How many leading segments identify the item.</param>
    /// <returns>The item, or <see langword="null"/>.</returns>
    private static AutomationElement? Find(AttachContext context, IReadOnlyList<string> segments, int count)
    {
        var id = IdOf(segments, count);
        var segment = segments[count - 1];
        foreach (var root in context.Roots())
        {
            var items = UiActions.Safe(() => root.FindAllDescendants(cf => cf.ByControlType(ControlType.MenuItem)), []);
            var match = items.FirstOrDefault(e => UiActions.Safe(() => e.AutomationId, string.Empty) == id)
                ?? items.FirstOrDefault(e => NameMatches(UiActions.Safe(() => e.Name, string.Empty), segment)
                    || UiActions.Safe(() => e.AutomationId, string.Empty) == segment);
            if (match is not null)
            {
                return match;
            }
        }

        return null;
    }

    /// <summary>
    ///     Opens a submenu or invokes a leaf item.
    /// </summary>
    /// <param name="item">The menu item.</param>
    /// <param name="invoke">Whether this is the last segment, so the item is invoked rather than expanded.</param>
    private static void Open(AutomationElement item, bool invoke)
    {
        if (!invoke && item.Patterns.ExpandCollapse.IsSupported)
        {
            item.Patterns.ExpandCollapse.Pattern.Expand();
        }
        else if (item.Patterns.Invoke.IsSupported)
        {
            item.Patterns.Invoke.Pattern.Invoke();
        }
        else if (item.Patterns.ExpandCollapse.IsSupported)
        {
            item.Patterns.ExpandCollapse.Pattern.Expand();
        }
        else
        {
            item.Click();
        }
    }

    /// <summary>
    ///     Removes access-key markers and white space.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <returns>The compact text.</returns>
    private static string Compact(string text) =>
        new([.. text.Where(c => !char.IsWhiteSpace(c) && c is not '_' and not '&')]);
}

/// <summary>
///     The outcome of <see cref="MenuNavigator.Invoke"/>.
/// </summary>
/// <param name="InvokedId">The AutomationId of the item invoked (or found disabled), or <see langword="null"/> when an item was missing.</param>
/// <param name="FailedSegment">The path segment that could not be used, or <see langword="null"/> on success.</param>
/// <param name="Disabled">Whether <paramref name="FailedSegment"/> was found but is disabled.</param>
internal sealed record MenuResult(string? InvokedId, string? FailedSegment, bool Disabled)
{
    /// <summary>
    ///     Gets a value indicating whether the whole path was walked.
    /// </summary>
    internal bool Succeeded => FailedSegment is null;
}
