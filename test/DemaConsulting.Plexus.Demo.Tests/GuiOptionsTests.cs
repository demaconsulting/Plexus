using DemaConsulting.Plexus.Demo.Gui;

namespace DemaConsulting.Plexus.Demo.Tests;

/// <summary>
///     Tests for <see cref="GuiOptions"/> command-line parsing.
/// </summary>
public class GuiOptionsTests
{
    /// <summary>
    ///     Parses arguments that must be accepted.
    /// </summary>
    /// <param name="args">The arguments.</param>
    /// <returns>The parsed options.</returns>
    private static GuiOptions Parse(params string[] args)
    {
        Assert.True(GuiOptions.TryParse(args, out var options, out var error), error);
        return options!;
    }

    /// <summary>
    ///     Parses arguments that must be rejected.
    /// </summary>
    /// <param name="args">The arguments.</param>
    /// <returns>The error message.</returns>
    private static string Reject(params string[] args)
    {
        Assert.False(GuiOptions.TryParse(args, out var options, out var error));
        Assert.Null(options);
        return error!;
    }

    /// <summary>
    ///     Validates that no arguments mean the full type set, no automation and no explicit document.
    /// </summary>
    [Fact]
    public void GuiOptions_TryParse_NoArguments_UsesDefaults()
    {
        // Arrange / Act
        var options = Parse();

        // Assert
        Assert.Null(options.OpenFile);
        Assert.Null(options.Sample);
        Assert.Equal(NodeTypeSet.Full, options.Types);
        Assert.Null(options.AutomationName);
        Assert.False(options.ShowHelp);
        Assert.False(options.HasExplicitDocument);
        Assert.Equal("showcase", GuiOptions.DefaultSample);
    }

    /// <summary>
    ///     Validates that --open records the file and counts as an explicit document.
    /// </summary>
    [Fact]
    public void GuiOptions_TryParse_Open_RecordsFile()
    {
        // Arrange / Act
        var options = Parse("--open", "my diagram.json");

        // Assert
        Assert.Equal("my diagram.json", options.OpenFile);
        Assert.True(options.HasExplicitDocument);
    }

    /// <summary>
    ///     Validates that --sample accepts any casing and normalizes to the sample's name.
    /// </summary>
    [Fact]
    public void GuiOptions_TryParse_SampleInAnyCase_IsNormalized()
    {
        // Arrange / Act
        var options = Parse("--sample", "PlumBing");

        // Assert
        Assert.Equal("plumbing", options.Sample);
        Assert.True(options.HasExplicitDocument);
    }

    /// <summary>
    ///     Validates that an unknown sample is rejected and the error lists the real ones.
    /// </summary>
    [Fact]
    public void GuiOptions_TryParse_UnknownSample_ListsAvailableSamples()
    {
        // Arrange / Act
        var error = Reject("--sample", "nope");

        // Assert
        Assert.Contains("nope", error, StringComparison.Ordinal);
        Assert.Contains("showcase", error, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Validates that --types accepts full and basic in any case.
    /// </summary>
    /// <param name="value">The option value.</param>
    /// <param name="expected">The expected set.</param>
    [Theory]
    [InlineData("full", NodeTypeSet.Full)]
    [InlineData("basic", NodeTypeSet.Basic)]
    [InlineData("BASIC", NodeTypeSet.Basic)]
    public void GuiOptions_TryParse_Types_SelectsSet(string value, NodeTypeSet expected)
    {
        // Arrange / Act
        var options = Parse("--types", value);

        // Assert
        Assert.Equal(expected, options.Types);
    }

    /// <summary>
    ///     Validates that unknown and numeric type sets are rejected.
    /// </summary>
    /// <param name="value">The option value.</param>
    [Theory]
    [InlineData("huge")]
    [InlineData("7")]
    public void GuiOptions_TryParse_BadTypes_IsRejected(string value)
    {
        // Arrange / Act
        var error = Reject("--types", value);

        // Assert
        Assert.Contains("type set", error, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Validates that --automation records the pipe name suffix.
    /// </summary>
    [Fact]
    public void GuiOptions_TryParse_Automation_RecordsName()
    {
        // Arrange / Act
        var options = Parse("--automation", "alpha-1_b");

        // Assert
        Assert.Equal("alpha-1_b", options.AutomationName);
    }

    /// <summary>
    ///     Validates that automation names that could escape the pipe namespace are rejected.
    /// </summary>
    /// <param name="value">The option value.</param>
    [Theory]
    [InlineData("a b")]
    [InlineData("..\\evil")]
    [InlineData("x/y")]
    public void GuiOptions_TryParse_BadAutomationName_IsRejected(string value)
    {
        // Arrange / Act
        var error = Reject("--automation", value);

        // Assert
        Assert.Contains("Automation name", error, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Validates that a file and a sample cannot both be requested.
    /// </summary>
    [Fact]
    public void GuiOptions_TryParse_OpenAndSample_IsRejected()
    {
        // Arrange / Act
        var error = Reject("--open", "a.json", "--sample", "math");

        // Assert
        Assert.Contains("either", error, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Validates that an option without its value is rejected.
    /// </summary>
    [Fact]
    public void GuiOptions_TryParse_MissingValue_IsRejected()
    {
        // Arrange / Act
        var error = Reject("--automation");

        // Assert
        Assert.Contains("needs a value", error, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Validates that unknown arguments are rejected.
    /// </summary>
    [Fact]
    public void GuiOptions_TryParse_UnknownArgument_IsRejected()
    {
        // Arrange / Act
        var error = Reject("--bogus");

        // Assert
        Assert.Contains("--bogus", error, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Validates that help is recognized in both spellings.
    /// </summary>
    /// <param name="flag">The help flag.</param>
    [Theory]
    [InlineData("-h")]
    [InlineData("--help")]
    public void GuiOptions_TryParse_Help_IsRecognized(string flag)
    {
        // Arrange / Act
        var options = Parse(flag);

        // Assert
        Assert.True(options.ShowHelp);
        Assert.Contains("--automation", GuiOptions.Usage, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Validates that all options can be combined.
    /// </summary>
    [Fact]
    public void GuiOptions_TryParse_Combined_AreAllParsed()
    {
        // Arrange / Act
        var options = Parse("--sample", "math", "--types", "basic", "--automation", "t1");

        // Assert
        Assert.Equal("math", options.Sample);
        Assert.Equal(NodeTypeSet.Basic, options.Types);
        Assert.Equal("t1", options.AutomationName);
    }
}
