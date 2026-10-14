using System.Text;
using System.Text.Json.Nodes;

namespace DemaConsulting.Plexus.Puppet;

/// <summary>
///     Turns the JSON results of the automation pipe into the plain text the Puppet prints.
/// </summary>
internal static class PipeFormat
{
    /// <summary>
    ///     Formats one scalar or nested value on a single line.
    /// </summary>
    /// <param name="node">The value.</param>
    /// <returns>Scalars as text (quoted when they contain white space, quotes or equals signs), containers as compact JSON.</returns>
    internal static string Value(JsonNode? node)
    {
        if (node is null)
        {
            return "null";
        }

        if (node is JsonValue value)
        {
            if (value.TryGetValue<string>(out var text))
            {
                return NeedsQuotes(text) ? "\"" + text.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("\"", "\\\"", StringComparison.Ordinal) + "\"" : text;
            }

            return value.ToJsonString();
        }

        return node.ToJsonString();
    }

    /// <summary>
    ///     Formats an object as one line of <c>key=value</c> pairs.
    /// </summary>
    /// <param name="item">The object.</param>
    /// <param name="omit">Member names to leave out (bulky members such as <c>route</c>).</param>
    /// <returns>The line.</returns>
    internal static string Item(JsonObject item, params string[] omit)
    {
        var parts = item.Where(pair => !omit.Contains(pair.Key)).Select(pair => $"{pair.Key}={Value(pair.Value)}");
        return string.Join(' ', parts);
    }

    /// <summary>
    ///     Formats a command result as lines of text.
    /// </summary>
    /// <remarks>
    ///     An object becomes one <c>key=value</c> line per member; an array becomes one line per item. List results of
    ///     nodes and wires drop their bulky members (ports, routes) unless <paramref name="verbose"/> is set.
    /// </remarks>
    /// <param name="command">The pipe command that produced the result.</param>
    /// <param name="result">The result.</param>
    /// <param name="verbose">Keep bulky members.</param>
    /// <returns>The text, without a trailing newline.</returns>
    internal static string Result(string command, JsonNode? result, bool verbose = false)
    {
        string[] omit = verbose ? [] : OmittedMembers(command);
        var builder = new StringBuilder();
        switch (result)
        {
            case JsonArray array when array.Count == 0:
                builder.Append("(none)");
                break;
            case JsonArray array:
                builder.AppendJoin('\n', array.Select(item => item is JsonObject o ? Item(o, omit) : Value(item)));
                break;
            case JsonObject obj:
                builder.AppendJoin('\n', obj.Where(p => !omit.Contains(p.Key)).Select(p => $"{p.Key}={Value(p.Value)}"));
                break;
            default:
                builder.Append(Value(result));
                break;
        }

        return builder.ToString();
    }

    /// <summary>
    ///     Gets the members a list command's summary leaves out.
    /// </summary>
    /// <param name="command">The pipe command.</param>
    /// <returns>The member names.</returns>
    private static string[] OmittedMembers(string command) => command switch
    {
        "list-nodes" => ["ports", "data"],
        "list-wires" => ["route"],
        _ => [],
    };

    /// <summary>
    ///     Tests whether a text must be quoted to stay one token.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <returns><see langword="true"/> when it is empty or holds white space, quotes or equals signs.</returns>
    private static bool NeedsQuotes(string text) =>
        text.Length == 0 || text.Any(c => char.IsWhiteSpace(c) || c is '"' or '=');
}
