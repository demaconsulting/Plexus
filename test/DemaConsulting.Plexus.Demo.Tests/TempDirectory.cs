namespace DemaConsulting.Plexus.Demo.Tests;

/// <summary>
///     A scratch directory that is deleted when disposed, so file-based tests leave nothing behind.
/// </summary>
internal sealed class TempDirectory : IDisposable
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="TempDirectory"/> class. The directory itself is
    ///     not created until a test needs it (see <see cref="Create"/>).
    /// </summary>
    public TempDirectory() => Path = System.IO.Path.Join(System.IO.Path.GetTempPath(), $"plexus-demo-{Guid.NewGuid():N}");

    /// <summary>
    ///     Gets the full path of the directory.
    /// </summary>
    public string Path { get; }

    /// <summary>
    ///     Creates the directory and returns its path.
    /// </summary>
    /// <returns>The directory path.</returns>
    public string Create()
    {
        Directory.CreateDirectory(Path);
        return Path;
    }

    /// <summary>
    ///     Deletes the directory and everything in it, if it exists.
    /// </summary>
    public void Dispose()
    {
        if (Directory.Exists(Path))
        {
            Directory.Delete(Path, recursive: true);
        }
    }
}
