using System.Globalization;
using System.Text.Json.Nodes;
using DemaConsulting.Plexus.Puppet.Automation;
using FlaUI.Core.AutomationElements;

namespace DemaConsulting.Plexus.Puppet;

/// <summary>
///     The commands of <c>plexus-puppet</c>; this part holds the helpers they share.
/// </summary>
/// <remarks>
///     Each command is one static method that does one thing and returns its exit code. Failures that should end the
///     command with a specific code are thrown as <see cref="PuppetException"/> and turned into a message and an exit
///     code by <c>Program</c>.
/// </remarks>
internal static partial class Commands
{
    /// <summary>
    ///     Attaches to the instance a command addresses.
    /// </summary>
    /// <param name="parsed">The parsed command.</param>
    /// <returns>The attachment; the caller disposes it.</returns>
    /// <exception cref="PuppetException">Thrown when the instance is not running or has no window.</exception>
    private static AttachContext Attach(ParsedCommand parsed)
    {
        var alias = ValidAlias(parsed);
        var process = DemoProcess.TryGetProcess(alias)
            ?? throw new PuppetException(ExitCodes.NotRunning, "NOT_RUNNING", $"Instance '{alias}' is not running (no answer on pipe {PipeNames.ForAlias(alias)}).");
        return AttachContext.TryAttach(alias, process, out var error)
            ?? throw new PuppetException(ExitCodes.NotRunning, "ATTACH_FAILED", error);
    }

    /// <summary>
    ///     Gets the instance alias of a command, checked.
    /// </summary>
    /// <param name="parsed">The parsed command.</param>
    /// <returns>The alias.</returns>
    /// <exception cref="PuppetException">Thrown when the alias is not valid.</exception>
    private static string ValidAlias(ParsedCommand parsed) =>
        PipeNames.IsValidAlias(parsed.Instance)
            ? parsed.Instance
            : throw PuppetException.Usage($"'{parsed.Instance}' is not a valid instance alias (letters, digits, '-' and '_').");

    /// <summary>
    ///     Sends one command over the automation pipe and returns its result.
    /// </summary>
    /// <param name="alias">The instance alias.</param>
    /// <param name="command">The pipe command.</param>
    /// <param name="arguments">The arguments, or <see langword="null"/>.</param>
    /// <returns>The <c>result</c> of the reply (may be <see langword="null"/>).</returns>
    /// <exception cref="PuppetException">Thrown when the instance does not answer or rejects the command.</exception>
    private static JsonNode? Call(string alias, string command, JsonObject? arguments = null)
    {
        PipeReply reply;
        try
        {
            reply = PipeClient.Send(alias, command, arguments);
        }
        catch (PipeUnavailableException ex)
        {
            throw new PuppetException(ExitCodes.NotRunning, "NOT_RUNNING", ex.Message);
        }

        return reply.Ok
            ? reply.Result
            : throw new PuppetException(ExitCodes.PipeError, "PIPE_ERROR", $"{command}: {reply.Error}");
    }

    /// <summary>
    ///     Requires a number of positional arguments.
    /// </summary>
    /// <param name="parsed">The parsed command.</param>
    /// <param name="count">The minimum number.</param>
    /// <param name="usage">The usage text printed when too few were given.</param>
    /// <exception cref="PuppetException">Thrown when there are too few arguments.</exception>
    private static void RequireArgs(ParsedCommand parsed, int count, string usage)
    {
        if (parsed.Positional.Count < count)
        {
            throw PuppetException.Usage(usage);
        }
    }

    /// <summary>
    ///     Parses a number argument.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <param name="what">What the number is, for the message.</param>
    /// <returns>The value.</returns>
    /// <exception cref="PuppetException">Thrown when the text is not a number.</exception>
    private static double Number(string text, string what) =>
        double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var value)
            ? value
            : throw PuppetException.Usage($"{what} must be a number, not '{text}'.");

    /// <summary>
    ///     Reads an integer option.
    /// </summary>
    /// <param name="parsed">The parsed command.</param>
    /// <param name="name">The option name.</param>
    /// <param name="fallback">The value when the option is absent.</param>
    /// <returns>The value.</returns>
    /// <exception cref="PuppetException">Thrown when the option is not an integer.</exception>
    private static int IntOption(ParsedCommand parsed, string name, int fallback)
    {
        if (parsed.GetOption(name) is not { } text)
        {
            return fallback;
        }

        return int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value)
            ? value
            : throw PuppetException.Usage($"--{name} must be an integer, not '{text}'.");
    }

    /// <summary>
    ///     Runs an action on the element with an AutomationId.
    /// </summary>
    /// <param name="parsed">The parsed command.</param>
    /// <param name="automationId">The AutomationId.</param>
    /// <param name="action">The action; it returns the exit code.</param>
    /// <returns>The exit code of the action.</returns>
    /// <exception cref="PuppetException">Thrown when there is no instance or no such element.</exception>
    private static int WithElement(ParsedCommand parsed, string automationId, Func<AttachContext, AutomationElement, int> action)
    {
        using var context = Attach(parsed);
        var element = ElementFinder.FindById(context, automationId)
            ?? throw new PuppetException(ExitCodes.NotFound, "NOT_FOUND", ElementFinder.BuildNotFoundMessage(context, automationId));
        return action(context, element);
    }

    /// <summary>
    ///     Prints a line to standard output and returns success.
    /// </summary>
    /// <param name="line">The line.</param>
    /// <returns><see cref="ExitCodes.Success"/>.</returns>
    private static int Ok(string line)
    {
        Console.WriteLine(line);
        return ExitCodes.Success;
    }
}
