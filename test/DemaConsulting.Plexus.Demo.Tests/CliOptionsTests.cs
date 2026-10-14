using DemaConsulting.Plexus.Demo.Cli;

namespace DemaConsulting.Plexus.Demo.Tests;

/// <summary>
///     Tests for <see cref="CliOptions"/> argument parsing.
/// </summary>
public class CliOptionsTests
{
    /// <summary>
    ///     Parses arguments that must be accepted.
    /// </summary>
    /// <param name="args">The arguments.</param>
    /// <returns>The parsed options.</returns>
    private static CliOptions Parse(params string[] args)
    {
        Assert.True(CliOptions.TryParse(args, out var options, out var error), error);
        return options!;
    }

    /// <summary>
    ///     Validates that a file input with an output directory parses with the defaults.
    /// </summary>
    [Fact]
    public void CliOptions_TryParse_FileAndOut_UsesDefaults()
    {
        // Arrange / Act
        var options = Parse("diagram.json", "--out", "pngs");

        // Assert
        Assert.Equal("diagram.json", options.InputFile);
        Assert.Equal("pngs", options.OutputDirectory);
        Assert.Null(options.Sample);
        Assert.Null(options.Page);
        Assert.Equal(1, options.Scale);
        Assert.True(options.ShowGrid);
        Assert.Null(options.WriteJson);
    }

    /// <summary>
    ///     Validates that every option is recognized.
    /// </summary>
    [Fact]
    public void CliOptions_TryParse_AllOptions_AreParsed()
    {
        // Arrange / Act
        var options = Parse("--sample", "math", "--out", "o", "--page", "2", "--scale", "1.5", "--no-grid", "--write-json", "x.json");

        // Assert
        Assert.Equal("math", options.Sample);
        Assert.Equal("2", options.Page);
        Assert.Equal(1.5, options.Scale);
        Assert.False(options.ShowGrid);
        Assert.Equal("x.json", options.WriteJson);
    }

    /// <summary>
    ///     Validates that help and listing need no input or output.
    /// </summary>
    [Fact]
    public void CliOptions_TryParse_HelpAndListSamples_StandAlone()
    {
        // Arrange / Act / Assert
        Assert.True(Parse("--help").ShowHelp);
        Assert.True(Parse("-h").ShowHelp);
        Assert.True(Parse("--list-samples").ListSamples);
    }

    /// <summary>
    ///     Validates that bad command lines are rejected with a message.
    /// </summary>
    /// <param name="arguments">The space-separated arguments.</param>
    /// <param name="fragment">Text the error message must contain.</param>
    [Theory]
    [InlineData("--out o", "input file")]
    [InlineData("a.json --out o --sample math", "not both")]
    [InlineData("a.json", "--out")]
    [InlineData("a.json b.json --out o", "only one input")]
    [InlineData("a.json --out", "needs a value")]
    [InlineData("a.json --out o --scale 0", "Invalid scale")]
    [InlineData("a.json --out o --scale abc", "Invalid scale")]
    [InlineData("a.json --out o --scale NaN", "Invalid scale")]
    [InlineData("a.json --out o --bogus", "Unknown option")]
    public void CliOptions_TryParse_InvalidArguments_ReportsError(string arguments, string fragment)
    {
        // Arrange
        var args = arguments.Split(' ');

        // Act
        var ok = CliOptions.TryParse(args, out var options, out var error);

        // Assert
        Assert.False(ok);
        Assert.Null(options);
        Assert.Contains(fragment, error, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    ///     Validates that a write-json-only command line is accepted (no PNG output requested).
    /// </summary>
    [Fact]
    public void CliOptions_TryParse_WriteJsonWithoutOut_IsAccepted()
    {
        // Arrange / Act
        var options = Parse("--sample", "audio", "--write-json", "b.json");

        // Assert
        Assert.Null(options.OutputDirectory);
        Assert.Equal("b.json", options.WriteJson);
    }
}
