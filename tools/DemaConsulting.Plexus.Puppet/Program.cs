using DemaConsulting.Plexus.Puppet;

namespace DemaConsulting.Plexus.Puppet;

/// <summary>
///     The entry point of <c>plexus-puppet</c>, a stateless driver for the Plexus demo application.
/// </summary>
internal static class Program
{
    /// <summary>
    ///     The commands and the methods that run them.
    /// </summary>
    private static readonly Dictionary<string, Func<ParsedCommand, int>> Handlers = new(StringComparer.OrdinalIgnoreCase)
    {
        ["launch"] = Commands.Launch,
        ["instances"] = _ => Commands.Instances(),
        ["quit"] = Commands.Quit,
        ["window"] = Commands.Window,
        ["tree"] = Commands.Tree,
        ["find"] = Commands.Find,
        ["click"] = Commands.Click,
        ["type"] = Commands.Type,
        ["choose"] = Commands.Choose,
        ["key"] = Commands.Key,
        ["read"] = Commands.Read,
        ["menu"] = Commands.Menu,
        ["dialogs"] = Commands.Dialogs,
        ["shot"] = Commands.Shot,
        ["place"] = Commands.Place,
        ["pipe"] = Commands.Pipe,
        ["info"] = Commands.Info,
        ["pages"] = Commands.Pages,
        ["nodes"] = Commands.Nodes,
        ["wires"] = Commands.Wires,
        ["select"] = Commands.Select,
        ["add-node"] = Commands.AddNode,
        ["connect"] = Commands.Connect,
        ["undo"] = Commands.Undo,
        ["redo"] = Commands.Redo,
        ["render"] = Commands.Render,
        ["drag-node"] = Commands.DragNode,
        ["ctrl-drag-node"] = Commands.CtrlDragNode,
        ["double-click-node"] = Commands.DoubleClickNode,
        ["double-click-name"] = Commands.DoubleClickName,
        ["right-click-node"] = Commands.RightClickNode,
        ["click-node"] = Commands.ClickNode,
        ["click-toggle"] = Commands.ClickToggle,
        ["drag-connect"] = Commands.DragConnect,
        ["drag-palette"] = Commands.DragPalette,
        ["click-at"] = Commands.ClickAt,
        ["drag-at"] = Commands.DragAt,
        ["drag-element"] = Commands.DragElement,
        ["wheel"] = Commands.Wheel,
        ["hover-node"] = Commands.HoverNode,
        ["hover-port"] = Commands.HoverPort,
        ["hover-wire"] = Commands.HoverWire,
        ["hover-at"] = Commands.HoverAt,
    };

    /// <summary>
    ///     Runs one command.
    /// </summary>
    /// <param name="args">The command line.</param>
    /// <returns>The exit code (see the README).</returns>
    public static int Main(string[] args)
    {
        DpiAwareness.Enable();
        if (args.Length == 0 || args[0] is "--help" or "-h" or "help")
        {
            PrintUsage();
            return args.Length == 0 ? ExitCodes.UsageError : ExitCodes.Success;
        }

        var parsed = CommandLineParser.Parse(args);
        if (!Handlers.TryGetValue(parsed.CommandName, out var handler))
        {
            Console.Error.WriteLine($"USAGE: Unknown command '{parsed.CommandName}'.");
            PrintUsage();
            return ExitCodes.UsageError;
        }

        try
        {
            return handler(parsed);
        }
        catch (PuppetException ex)
        {
            Console.Error.WriteLine($"{ex.Label}: {ex.Message}");
            return ex.ExitCode;
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            Console.Error.WriteLine($"ERROR: {parsed.CommandName} failed: {ex.Message}");
            if (parsed.HasOption("trace"))
            {
                Console.Error.WriteLine(ex.ToString());
            }

            return ExitCodes.Error;
        }
    }

    /// <summary>
    ///     Prints the command overview to standard error.
    /// </summary>
    private static void PrintUsage() => Console.Error.WriteLine(UsageText);

    /// <summary>
    ///     The command overview.
    /// </summary>
    private const string UsageText = """
        plexus-puppet - stateless driver for the Plexus demo (one action per process).

        WARNING: commands drive the real desktop. Do not use the keyboard or mouse while a run is active.

        Every command accepts --instance <alias> (default 'main').

        INSTANCES   launch [--instance a] [--sample <name> | --open <file>] [--types full|basic] [--exe <path>] [--allow-existing]
                    instances | quit [--kill]
        UI          window | tree [--depth N] [--under <id>] | find <id> | click <id> | type <id> "<text>" | choose <id> "<item>"
                    key <chord> [--to <id>] | read <id> | menu <Menu>/<Item>[/<Sub>] | dialogs | shot <path.png> [--screen]
                    place (--x <px> --y <px> --width <px> --height <px> | --work-area [--left-gap <px>] [--margin <px>])
        PIPE        pipe <cmd> [<json>] | info | pages | nodes [--page p] [--ports] | wires [--page p] [--ports]
                    select <names...> [--add] | add-node <type> <x> <y> [name] | connect <Node.port> <Node.port>
                    undo | redo | render <path.png> [--page p] [--scale s]
        MOUSE       drag-node <n> <dx> <dy> | ctrl-drag-node <n> <dx> <dy> | double-click-node <n> | double-click-name <n>
                    right-click-node <n> | click-node <n> [--shift|--ctrl] | click-toggle <group> | drag-connect <N.port> <N.port>
                    drag-palette <type> <x> <y> | click-at <x> <y> | drag-at <x1> <y1> <x2> <y2> | wheel <notches> [--ctrl] [--at x,y]
                    (diagram coordinates unless stated) | drag-element <automationId> <dx> <dy> (physical screen pixels)
        HOVER       hover-node <n> | hover-port <N.port[@in|@out]> | hover-wire (<wire id> | <N.port> <N.port>) | hover-at <x> <y>
                    each with [--wait <ms> (default 700)] [--shot <path.png> (screen copy while the tooltip shows)]

        Exit codes: 0 ok, 1 not found, 2 timeout, 3 not running, 4 launch failed, 5 usage, 6 disabled, 7 no focus,
                    8 no effect, 9 read-only, 10 dialog open, 11 obscured (input refused), 12 pipe command rejected, 99 error.
        """;
}
