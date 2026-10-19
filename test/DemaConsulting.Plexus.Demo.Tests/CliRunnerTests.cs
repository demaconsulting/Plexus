using DemaConsulting.CanvasNet.Codecs;
using DemaConsulting.Plexus.Demo.Cli;
using DemaConsulting.Plexus.Demo.Model;

namespace DemaConsulting.Plexus.Demo.Tests;

/// <summary>
///     Tests for <see cref="CliRunner"/>: rendering samples and files to PNG, page selection, exit codes and messages.
/// </summary>
public class CliRunnerTests
{
    /// <summary>
    ///     Runs the tool and captures its output.
    /// </summary>
    /// <param name="args">The arguments.</param>
    /// <returns>The exit code, standard output and standard error.</returns>
    private static (int Code, string Output, string Error) Run(params string[] args)
    {
        using var output = new StringWriter();
        using var error = new StringWriter();
        var code = CliRunner.Run(args, output, error);
        return (code, output.ToString(), error.ToString());
    }

    /// <summary>
    ///     Validates that rendering a single-page sample writes one valid PNG named after the page.
    /// </summary>
    [Fact]
    public void CliRunner_Run_SingleSample_WritesOnePng()
    {
        // Arrange
        using var temp = new TempDirectory();

        // Act
        var (code, output, _) = Run("--sample", "plumbing", "--out", temp.Path);

        // Assert
        Assert.Equal(CliRunner.Success, code);
        var file = Path.Join(temp.Path, "Plumbing.png");
        Assert.True(File.Exists(file));
        Assert.Contains("Plumbing.png", output, StringComparison.Ordinal);
        using var image = PngCodec.Load(file);
        Assert.True(image.Width > 100 && image.Height > 100);
    }

    /// <summary>
    ///     Validates that the showcase renders every page, and that scale and the grid switch are honored.
    /// </summary>
    [Fact]
    public void CliRunner_Run_ShowcaseWithScaleAndNoGrid_WritesEveryPage()
    {
        // Arrange
        using var temp = new TempDirectory();
        var scaled = Path.Join(temp.Path, "x2");
        var plain = Path.Join(temp.Path, "x1");

        // Act
        var (codeScaled, _, _) = Run("--sample", "showcase", "--out", scaled, "--scale", "2", "--no-grid");
        var (codePlain, _, _) = Run("--sample", "showcase", "--out", plain);

        // Assert: seven pages; the 2x images are about twice the size
        Assert.Equal(0, codeScaled + codePlain);
        Assert.Equal(["Audio.png", "Groups.png", "Logic.png", "Math.png", "Plumbing.png", "Wiring.png", "Workflow.png"], Directory.GetFiles(scaled).Select(Path.GetFileName).Order().ToArray());
        using var small = PngCodec.Load(Path.Join(plain, "Audio.png"));
        using var large = PngCodec.Load(Path.Join(scaled, "Audio.png"));
        Assert.InRange(large.Width, (small.Width * 2) - 2, (small.Width * 2) + 2);
    }

    /// <summary>
    ///     Validates that pages can be selected by name or by zero-based index.
    /// </summary>
    /// <param name="selector">The page name or index.</param>
    [Theory]
    [InlineData("Audio")]
    [InlineData("audio")]
    [InlineData("3")]
    public void CliRunner_Run_PageSelector_RendersOnlyThatPage(string selector)
    {
        // Arrange
        using var temp = new TempDirectory();

        // Act
        var (code, _, _) = Run("--sample", "showcase", "--out", temp.Path, "--page", selector);

        // Assert
        Assert.Equal(CliRunner.Success, code);
        Assert.Equal(["Audio.png"], Directory.GetFiles(temp.Path).Select(Path.GetFileName).ToArray());
    }

    /// <summary>
    ///     Validates that an unknown page fails with a message listing the pages and writes nothing.
    /// </summary>
    [Fact]
    public void CliRunner_Run_UnknownPage_FailsAndWritesNothing()
    {
        // Arrange
        using var temp = new TempDirectory();

        // Act
        var (code, _, error) = Run("--sample", "showcase", "--out", temp.Path, "--page", "nope");

        // Assert
        Assert.Equal(CliRunner.Failure, code);
        Assert.Contains("no page matches 'nope'", error, StringComparison.Ordinal);
        Assert.Contains("Audio", error, StringComparison.Ordinal);
        Assert.False(Directory.Exists(temp.Path));
    }

    /// <summary>
    ///     Validates that --write-json saves a diagram that renders to the same pages when loaded back from a file.
    /// </summary>
    [Fact]
    public void CliRunner_Run_WriteJsonThenRenderFile_RoundTrips()
    {
        // Arrange
        using var temp = new TempDirectory();
        var root = temp.Create();
        var json = Path.Join(root, "math.json");
        var fromSample = Path.Join(root, "sample");
        var fromFile = Path.Join(root, "file");

        // Act
        var (writeCode, _, _) = Run("--sample", "math", "--out", fromSample, "--write-json", json);
        var (fileCode, _, _) = Run(json, "--out", fromFile);

        // Assert: identical output from the sample and from its saved JSON
        Assert.Equal(0, writeCode + fileCode);
        Assert.Equal(File.ReadAllBytes(Path.Join(fromSample, "Math.png")), File.ReadAllBytes(Path.Join(fromFile, "Math.png")));
    }

    /// <summary>
    ///     Validates that a missing or malformed input file is a clear failure, not a crash.
    /// </summary>
    [Fact]
    public void CliRunner_Run_BadInputFile_FailsWithMessage()
    {
        // Arrange
        using var temp = new TempDirectory();
        var root = temp.Create();
        var broken = Path.Join(root, "broken.json");
        File.WriteAllText(broken, "{ not json");

        // Act
        var (missingCode, _, missingError) = Run(Path.Join(root, "missing.json"), "--out", root);
        var (brokenCode, _, brokenError) = Run(broken, "--out", root);

        // Assert
        Assert.Equal((CliRunner.Failure, CliRunner.Failure), (missingCode, brokenCode));
        Assert.StartsWith("error:", missingError, StringComparison.Ordinal);
        Assert.StartsWith("error:", brokenError, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Validates that an unknown sample is reported as a failure naming the known samples.
    /// </summary>
    [Fact]
    public void CliRunner_Run_UnknownSample_FailsListingSamples()
    {
        // Arrange
        using var temp = new TempDirectory();

        // Act
        var (code, _, error) = Run("--sample", "nope", "--out", temp.Path);

        // Assert
        Assert.Equal(CliRunner.Failure, code);
        Assert.Contains("math", error, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Validates the usage-error exit code and the help and list outputs.
    /// </summary>
    [Fact]
    public void CliRunner_Run_UsageHelpAndList_ReportExpectedOutput()
    {
        // Arrange / Act
        var (badCode, _, badError) = Run("--out");
        var (helpCode, helpOutput, _) = Run("--help");
        var (listCode, listOutput, _) = Run("--list-samples");

        // Assert
        Assert.Equal(CliRunner.UsageError, badCode);
        Assert.Contains("Usage:", badError, StringComparison.Ordinal);
        Assert.Equal(CliRunner.Success, helpCode);
        Assert.Contains("--sample", helpOutput, StringComparison.Ordinal);
        Assert.Equal(CliRunner.Success, listCode);
        Assert.Equal(DemoSamples.Names, listOutput.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries));
    }

    /// <summary>
    ///     Validates that page names become valid, unique file names.
    /// </summary>
    [Fact]
    public void CliRunner_UniqueFileName_InvalidAndDuplicateNames_AreMadeSafe()
    {
        // Arrange
        var used = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // Act
        var first = CliRunner.UniqueFileName("a/b:c", 0, used);
        var second = CliRunner.UniqueFileName("A/B:C", 1, used);
        var blank = CliRunner.UniqueFileName("  ", 2, used);

        // Assert
        Assert.Equal("a_b_c.png", first);
        Assert.Equal("A_B_C-1.png", second);
        Assert.Equal("page2.png", blank);
    }
}
