using DemaConsulting.Plexus.Puppet;
using FlaUI.Core.WindowsAPI;

namespace DemaConsulting.Plexus.Puppet.Tests;

/// <summary>
///     Tests for the command-line parsing and key-chord parsing of the Puppet (no UI is touched).
/// </summary>
public class CommandLineTests
{
    /// <summary>
    ///     Validates that the command name, positional arguments and valued options are separated.
    /// </summary>
    [Fact]
    public void CommandLineParser_Parse_SplitsCommandPositionalAndOptions()
    {
        // Arrange / Act
        var parsed = CommandLineParser.Parse(["type", "Properties_Name", "New name", "--instance", "a"]);

        // Assert
        Assert.Multiple(
            () => Assert.Equal("type", parsed.CommandName),
            () => Assert.Equal(["Properties_Name", "New name"], parsed.Positional),
            () => Assert.Equal("a", parsed.GetOption("instance")),
            () => Assert.Equal("a", parsed.Instance));
    }

    /// <summary>
    ///     Validates that no arguments give an empty command and the default instance alias.
    /// </summary>
    [Fact]
    public void CommandLineParser_Parse_NoArguments_IsEmptyWithDefaultInstance()
    {
        // Arrange / Act
        var parsed = CommandLineParser.Parse([]);

        // Assert
        Assert.Multiple(
            () => Assert.Equal(string.Empty, parsed.CommandName),
            () => Assert.Empty(parsed.Positional),
            () => Assert.Equal("main", parsed.Instance));
    }

    /// <summary>
    ///     Validates that known flags do not swallow the argument after them.
    /// </summary>
    [Fact]
    public void CommandLineParser_Parse_FlagBeforePositional_KeepsPositional()
    {
        // Arrange / Act
        var parsed = CommandLineParser.Parse(["select", "--add", "A", "B"]);

        // Assert
        Assert.Multiple(
            () => Assert.True(parsed.HasOption("add")),
            () => Assert.Null(parsed.GetOption("add")),
            () => Assert.Equal(["A", "B"], parsed.Positional));
    }

    /// <summary>
    ///     Validates that the double flag takes no value, so the coordinates after it stay positional.
    /// </summary>
    [Fact]
    public void CommandLineParser_Parse_DoubleFlag_DoesNotSwallowPositionals()
    {
        // Arrange / Act
        var parsed = CommandLineParser.Parse(["click-at", "--double", "645", "360"]);

        // Assert
        Assert.Multiple(
            () => Assert.True(parsed.HasOption("double")),
            () => Assert.Equal(["645", "360"], parsed.Positional));
    }

    /// <summary>
    ///     Validates that negative numbers are positional arguments, not options.
    /// </summary>
    [Fact]
    public void CommandLineParser_Parse_NegativeNumbers_ArePositional()
    {
        // Arrange / Act
        var parsed = CommandLineParser.Parse(["drag-node", "Mixer", "-40", "25", "--shift"]);

        // Assert
        Assert.Multiple(
            () => Assert.Equal(["Mixer", "-40", "25"], parsed.Positional),
            () => Assert.True(parsed.HasOption("shift")));
    }

    /// <summary>
    ///     Validates that an option value may start with a single dash and that option names are case-insensitive.
    /// </summary>
    [Fact]
    public void CommandLineParser_Parse_AtOptionWithNegativeValue_IsKept()
    {
        // Arrange / Act
        var parsed = CommandLineParser.Parse(["wheel", "3", "--at", "-10,20", "--CTRL"]);

        // Assert
        Assert.Multiple(
            () => Assert.Equal("-10,20", parsed.GetOption("AT")),
            () => Assert.True(parsed.HasOption("ctrl")),
            () => Assert.Equal(["3"], parsed.Positional));
    }

    /// <summary>
    ///     Validates that an option at the end of the line with no value is a flag.
    /// </summary>
    [Fact]
    public void CommandLineParser_Parse_TrailingOptionWithoutValue_IsFlag()
    {
        // Arrange / Act
        var parsed = CommandLineParser.Parse(["quit", "--whatever"]);

        // Assert
        Assert.Multiple(
            () => Assert.True(parsed.HasOption("whatever")),
            () => Assert.Null(parsed.GetOption("whatever")));
    }

    /// <summary>
    ///     Validates that simple chords parse to the expected virtual keys and canonical names.
    /// </summary>
    /// <param name="text">The chord text.</param>
    /// <param name="name">The expected canonical key name.</param>
    /// <param name="key">The expected virtual key.</param>
    [Theory]
    [InlineData("Delete", "Delete", VirtualKeyShort.DELETE)]
    [InlineData("del", "Delete", VirtualKeyShort.DELETE)]
    [InlineData("F2", "F2", VirtualKeyShort.F2)]
    [InlineData("f12", "F12", VirtualKeyShort.F12)]
    [InlineData("Escape", "Escape", VirtualKeyShort.ESCAPE)]
    [InlineData("esc", "Escape", VirtualKeyShort.ESCAPE)]
    [InlineData("Enter", "Enter", VirtualKeyShort.RETURN)]
    [InlineData("z", "Z", VirtualKeyShort.KEY_Z)]
    [InlineData("7", "7", VirtualKeyShort.KEY_7)]
    [InlineData("PageDown", "PageDown", VirtualKeyShort.NEXT)]
    [InlineData("Plus", "Plus", VirtualKeyShort.OEM_PLUS)]
    public void KeyChord_TryParse_SingleKey_ParsesWithoutModifiers(string text, string name, VirtualKeyShort key)
    {
        // Arrange / Act
        var ok = KeyChord.TryParse(text, out var chord, out var error);

        // Assert
        Assert.Multiple(
            () => Assert.True(ok, error),
            () => Assert.Equal(name, chord.KeyName),
            () => Assert.Equal(key, chord.Key),
            () => Assert.False(chord.HasModifiers));
    }

    /// <summary>
    ///     Validates that modifiers are collected in any order and formatted canonically.
    /// </summary>
    [Fact]
    public void KeyChord_TryParse_Modifiers_AreCombinedAndFormatted()
    {
        // Arrange / Act
        var ok = KeyChord.TryParse("shift+Control+z", out var chord, out var error);

        // Assert
        Assert.Multiple(
            () => Assert.True(ok, error),
            () => Assert.Equal(KeyChordModifiers.Control | KeyChordModifiers.Shift, chord.Modifiers),
            () => Assert.Equal("Ctrl+Shift+Z", chord.ToString()));
    }

    /// <summary>
    ///     Validates that Ctrl+Enter and Alt+F4-style chords parse.
    /// </summary>
    [Fact]
    public void KeyChord_TryParse_CtrlEnter_Parses()
    {
        // Arrange / Act
        var ok = KeyChord.TryParse("Ctrl+Enter", out var chord, out _);

        // Assert
        Assert.Multiple(
            () => Assert.True(ok),
            () => Assert.Equal(VirtualKeyShort.RETURN, chord.Key),
            () => Assert.Equal(KeyChordModifiers.Control, chord.Modifiers),
            () => Assert.Equal("Ctrl+Enter", chord.ToString()));
    }

    /// <summary>
    ///     Validates that bad chords are rejected with the usage text.
    /// </summary>
    /// <param name="text">The chord text.</param>
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("Hyper+C")]
    [InlineData("Ctrl+Banana")]
    [InlineData("F13")]
    [InlineData("Ctrl+")]
    public void KeyChord_TryParse_Invalid_IsRejected(string text)
    {
        // Arrange / Act
        var ok = KeyChord.TryParse(text, out _, out var error);

        // Assert
        Assert.Multiple(
            () => Assert.False(ok),
            () => Assert.Contains("Keys:", error, StringComparison.Ordinal));
    }

    /// <summary>
    ///     Validates that a null chord is rejected.
    /// </summary>
    [Fact]
    public void KeyChord_TryParse_Null_IsRejected()
    {
        // Arrange / Act
        var ok = KeyChord.TryParse(null, out _, out var error);

        // Assert
        Assert.Multiple(
            () => Assert.False(ok),
            () => Assert.NotEmpty(error));
    }
}
