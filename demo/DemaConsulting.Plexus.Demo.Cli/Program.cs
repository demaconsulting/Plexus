using DemaConsulting.Plexus.Demo.Cli;

// The tool is a thin shell around CliRunner so its behavior stays testable without a process
return CliRunner.Run(args, Console.Out, Console.Error);
