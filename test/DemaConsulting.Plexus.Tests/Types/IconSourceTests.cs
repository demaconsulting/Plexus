using System.Globalization;
using System.Reflection;

namespace DemaConsulting.Plexus.Tests;

/// <summary>
///     Unit tests for <see cref="IconSource"/>. Further evidence for the unit lives in <c>TypesAndGeometryTests.cs</c>.
/// </summary>
public class IconSourceTests
{
    /// <summary>
    ///     A minimal SVG document used across the tests.
    /// </summary>
    private const string Svg = "<svg xmlns='http://www.w3.org/2000/svg' width='10' height='10'/>";

    /// <summary>
    ///     Validates that null, empty and whitespace SVG text is rejected.
    /// </summary>
    /// <param name="svg">The blank text.</param>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void IconSource_FromSvg_BlankText_ThrowsArgumentException(string? svg)
    {
        // Act / Assert
        Assert.ThrowsAny<ArgumentException>(() => IconSource.FromSvg(svg!));
    }

    /// <summary>
    ///     Validates that the key is a prefixed hexadecimal digest of the content.
    /// </summary>
    [Fact]
    public void IconSource_Key_HasPrefixAndHexDigest()
    {
        // Act
        var key = IconSource.FromSvg(Svg).Key;

        // Assert: "svg:" followed by 24 uppercase hexadecimal digits
        Assert.StartsWith("svg:", key, StringComparison.Ordinal);
        Assert.Equal(4 + 24, key.Length);
        Assert.All(key[4..], c => Assert.True(char.IsAsciiHexDigitUpper(c) || char.IsAsciiDigit(c)));
    }

    /// <summary>
    ///     Validates that icons compare by content key and work as set members.
    /// </summary>
    [Fact]
    public void IconSource_Equality_FollowsContent()
    {
        // Arrange
        var first = IconSource.FromSvg(Svg);
        var same = IconSource.FromSvg(Svg);
        var other = IconSource.FromSvg("<svg/>");

        // Act
        var set = new HashSet<IconSource> { first, same, other };

        // Assert
        Assert.True(first.Equals(same));
        Assert.True(first.Equals((object)same));
        Assert.False(first.Equals(other));
        Assert.False(first.Equals((IconSource?)null));
        Assert.False(first.Equals("not an icon"));
        Assert.Equal(2, set.Count);
    }

    /// <summary>
    ///     Validates that an icon read from a file exposes the file content.
    /// </summary>
    [Fact]
    public void IconSource_FromSvgFile_ExistingFile_LoadsContent()
    {
        // Arrange: write a file beside the test binaries
        var path = Path.Combine(AppContext.BaseDirectory, string.Create(CultureInfo.InvariantCulture, $"icon-source-{Guid.NewGuid():N}.svg"));
        File.WriteAllText(path, Svg);

        try
        {
            // Act
            var icon = IconSource.FromSvgFile(path);

            // Assert: same content and key as an icon created from the text
            Assert.Equal(Svg, icon.SvgText);
            Assert.Equal(IconSource.FromSvg(Svg), icon);
        }
        finally
        {
            File.Delete(path);
        }
    }

    /// <summary>
    ///     Validates that a missing file surfaces as an I/O exception and a blank path as an argument exception.
    /// </summary>
    [Fact]
    public void IconSource_FromSvgFile_MissingFileOrBlankPath_Throws()
    {
        // Arrange
        var missing = Path.Combine(AppContext.BaseDirectory, "no-such-icon.svg");

        // Act / Assert
        Assert.ThrowsAny<IOException>(() => IconSource.FromSvgFile(missing));
        Assert.ThrowsAny<ArgumentException>(() => IconSource.FromSvgFile(" "));
        Assert.ThrowsAny<ArgumentException>(() => IconSource.FromSvgFile(null!));
    }

    /// <summary>
    ///     Validates that an icon loads from an embedded resource of an assembly.
    /// </summary>
    [Fact]
    public void IconSource_FromSvgResource_ExistingResource_LoadsContent()
    {
        // Arrange: an assembly exposing one resource
        var assembly = new ResourceAssembly("icons.node.svg", Svg);

        // Act
        var icon = IconSource.FromSvgResource(assembly, "icons.node.svg");

        // Assert
        Assert.Equal(Svg, icon.SvgText);
        Assert.Equal(IconSource.FromSvg(Svg), icon);
    }

    /// <summary>
    ///     Validates that a missing resource names the resource in the error and invalid arguments are rejected.
    /// </summary>
    [Fact]
    public void IconSource_FromSvgResource_MissingResourceOrInvalidArguments_Throws()
    {
        // Arrange
        var assembly = new ResourceAssembly("icons.node.svg", Svg);

        // Act
        var missing = Assert.Throws<ArgumentException>(() => IconSource.FromSvgResource(assembly, "icons.other.svg"));

        // Assert
        Assert.Contains("icons.other.svg", missing.Message, StringComparison.Ordinal);
        Assert.Throws<ArgumentNullException>(() => IconSource.FromSvgResource(null!, "icons.node.svg"));
        Assert.ThrowsAny<ArgumentException>(() => IconSource.FromSvgResource(assembly, " "));
    }

    /// <summary>
    ///     An assembly stand-in that serves one manifest resource from memory.
    /// </summary>
    private sealed class ResourceAssembly : Assembly
    {
        /// <summary>
        ///     The only resource name served.
        /// </summary>
        private readonly string _name;

        /// <summary>
        ///     The content of the resource.
        /// </summary>
        private readonly string _content;

        /// <summary>
        ///     Initializes a new instance of the <see cref="ResourceAssembly"/> class.
        /// </summary>
        /// <param name="name">The resource name.</param>
        /// <param name="content">The resource content.</param>
        public ResourceAssembly(string name, string content)
        {
            _name = name;
            _content = content;
        }

        /// <inheritdoc />
        public override AssemblyName GetName() => new("ResourceAssembly");

        /// <inheritdoc />
        public override Stream? GetManifestResourceStream(string name) =>
            name == _name ? new MemoryStream(System.Text.Encoding.UTF8.GetBytes(_content)) : null;
    }
}
