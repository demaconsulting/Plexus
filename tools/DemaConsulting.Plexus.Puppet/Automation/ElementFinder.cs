using System.Text;
using FlaUI.Core.AutomationElements;

namespace DemaConsulting.Plexus.Puppet.Automation;

/// <summary>
///     Finds elements by AutomationId across all the windows of an instance.
/// </summary>
internal static class ElementFinder
{
    /// <summary>
    ///     Finds the first element with an AutomationId, looking in the main window first, then in dialogs and pop-ups.
    /// </summary>
    /// <param name="context">The attached instance.</param>
    /// <param name="automationId">The AutomationId.</param>
    /// <returns>The element, or <see langword="null"/> when there is none.</returns>
    internal static AutomationElement? FindById(AttachContext context, string automationId)
    {
        foreach (var root in context.Roots())
        {
            var found = UiActions.Safe(() => root.FindFirstDescendant(cf => cf.ByAutomationId(automationId)), null);
            if (found is not null)
            {
                return found;
            }

            if (UiActions.Safe(() => root.AutomationId, string.Empty) == automationId)
            {
                return root;
            }
        }

        return null;
    }

    /// <summary>
    ///     Builds the "not found" message with the closest AutomationIds as suggestions.
    /// </summary>
    /// <param name="context">The attached instance.</param>
    /// <param name="automationId">The AutomationId that was asked for.</param>
    /// <returns>The message.</returns>
    internal static string BuildNotFoundMessage(AttachContext context, string automationId)
    {
        var ids = context.Roots().SelectMany(root => UiTreeDumper.CollectAutomationIds(root, 12)).Distinct(StringComparer.Ordinal).ToList();
        var suggestions = Suggest(automationId, ids);
        return suggestions.Count == 0
            ? $"'{automationId}' was not found; {ids.Count} automation ids are present."
            : $"'{automationId}' was not found. Did you mean: {string.Join(", ", suggestions)}?";
    }

    /// <summary>
    ///     Picks the identifiers closest to a misspelled one.
    /// </summary>
    /// <param name="wanted">The identifier that was asked for.</param>
    /// <param name="candidates">The identifiers that exist.</param>
    /// <returns>Up to three candidates, closest first.</returns>
    internal static IReadOnlyList<string> Suggest(string wanted, IEnumerable<string> candidates) =>
        [.. candidates
            .Select(id => (Id: id, Score: Distance(wanted, id)))
            .OrderBy(x => x.Score)
            .ThenBy(x => x.Id, StringComparer.Ordinal)
            .Take(3)
            .Select(x => x.Id)];

    /// <summary>
    ///     Computes the case-insensitive edit distance of two strings.
    /// </summary>
    /// <param name="left">The first string.</param>
    /// <param name="right">The second string.</param>
    /// <returns>The number of single-character edits between them.</returns>
    internal static int Distance(string left, string right)
    {
        var d = new int[left.Length + 1, right.Length + 1];
        for (var i = 0; i <= left.Length; i++)
        {
            d[i, 0] = i;
        }

        for (var j = 0; j <= right.Length; j++)
        {
            d[0, j] = j;
        }

        for (var i = 1; i <= left.Length; i++)
        {
            for (var j = 1; j <= right.Length; j++)
            {
                var cost = char.ToUpperInvariant(left[i - 1]) == char.ToUpperInvariant(right[j - 1]) ? 0 : 1;
                d[i, j] = Math.Min(Math.Min(d[i - 1, j] + 1, d[i, j - 1] + 1), d[i - 1, j - 1] + cost);
            }
        }

        return d[left.Length, right.Length];
    }
}

/// <summary>
///     Prints the UI Automation tree.
/// </summary>
internal static class UiTreeDumper
{
    /// <summary>
    ///     Dumps an element and its descendants, one indented line each.
    /// </summary>
    /// <param name="root">The root element.</param>
    /// <param name="maxDepth">How many levels below the root to show.</param>
    /// <returns>The text.</returns>
    internal static string Dump(AutomationElement root, int maxDepth)
    {
        var builder = new StringBuilder();
        Dump(root, builder, 0, maxDepth);
        return builder.ToString();
    }

    /// <summary>
    ///     Collects the AutomationIds below an element.
    /// </summary>
    /// <param name="root">The root element.</param>
    /// <param name="maxDepth">How many levels below the root to visit.</param>
    /// <returns>The distinct ids, sorted.</returns>
    internal static IReadOnlyList<string> CollectAutomationIds(AutomationElement root, int maxDepth)
    {
        var ids = new HashSet<string>(StringComparer.Ordinal);
        Collect(root, ids, 0, maxDepth);
        return [.. ids.OrderBy(id => id, StringComparer.Ordinal)];
    }

    /// <summary>
    ///     Writes one element and recurses into its children.
    /// </summary>
    /// <param name="element">The element.</param>
    /// <param name="builder">The output.</param>
    /// <param name="depth">The current depth.</param>
    /// <param name="maxDepth">The deepest level to show.</param>
    private static void Dump(AutomationElement element, StringBuilder builder, int depth, int maxDepth)
    {
        var id = UiActions.Safe(() => element.AutomationId, string.Empty);
        var name = UiActions.Safe(() => element.Name, string.Empty);
        builder.Append(' ', depth * 2).Append(UiActions.Safe(() => element.ControlType.ToString(), "?"));
        if (!string.IsNullOrWhiteSpace(id))
        {
            builder.Append(" id=").Append(id);
        }

        if (!string.IsNullOrWhiteSpace(name))
        {
            builder.Append(" name=\"").Append(name.Replace("\"", "\\\"", StringComparison.Ordinal)).Append('"');
        }

        if (!UiActions.Safe(() => element.IsEnabled, true))
        {
            builder.Append(" [disabled]");
        }

        builder.AppendLine();
        if (depth >= maxDepth)
        {
            return;
        }

        foreach (var child in UiActions.Safe(() => element.FindAllChildren(), []))
        {
            Dump(child, builder, depth + 1, maxDepth);
        }
    }

    /// <summary>
    ///     Adds the ids of an element and its descendants.
    /// </summary>
    /// <param name="element">The element.</param>
    /// <param name="ids">The ids found so far.</param>
    /// <param name="depth">The current depth.</param>
    /// <param name="maxDepth">The deepest level to visit.</param>
    private static void Collect(AutomationElement element, HashSet<string> ids, int depth, int maxDepth)
    {
        var id = UiActions.Safe(() => element.AutomationId, string.Empty);
        if (!string.IsNullOrWhiteSpace(id))
        {
            ids.Add(id);
        }

        if (depth >= maxDepth)
        {
            return;
        }

        foreach (var child in UiActions.Safe(() => element.FindAllChildren(), []))
        {
            Collect(child, ids, depth + 1, maxDepth);
        }
    }
}
