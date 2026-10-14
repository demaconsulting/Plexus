namespace DemaConsulting.Plexus.Demo.Model;

/// <summary>
///     Loads and saves demo diagrams as Plexus snapshot JSON, the demo's one file format
///     (shared by the command-line renderer and the demo GUI).
/// </summary>
/// <remarks>
///     The library does no file I/O of its own; a host wraps <see cref="Diagram.ToJson(bool)"/> and
///     <see cref="Diagram.FromJson(string, NodeTypeRegistry, DiagramOptions?)"/> exactly like this.
///     Loading needs the demo node types, so they are registered here.
/// </remarks>
public static class DemoDocument
{
    /// <summary>
    ///     Loads a diagram from a snapshot JSON file.
    /// </summary>
    /// <param name="path">The file to read. Must not be null or empty.</param>
    /// <returns>The loaded diagram with an empty undo history.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="path"/> is null or empty.</exception>
    /// <exception cref="IOException">Thrown when the file cannot be read.</exception>
    /// <exception cref="System.Text.Json.JsonException">Thrown when the file is not valid snapshot JSON.</exception>
    /// <exception cref="SnapshotException">Thrown when the snapshot cannot be restored (for example an unknown node type).</exception>
    public static Diagram Load(string path) => Load(path, DemoNodeTypes.CreateRegistry());

    /// <summary>
    ///     Loads a diagram from a snapshot JSON file using the given node types.
    /// </summary>
    /// <param name="path">The file to read. Must not be null or empty.</param>
    /// <param name="types">The node types the loaded diagram may use. Must not be null.</param>
    /// <returns>The loaded diagram with an empty undo history.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="path"/> is null or empty.</exception>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="types"/> is null.</exception>
    /// <exception cref="IOException">Thrown when the file cannot be read.</exception>
    /// <exception cref="System.Text.Json.JsonException">Thrown when the file is not valid snapshot JSON.</exception>
    /// <exception cref="SnapshotException">Thrown when the snapshot cannot be restored (for example an unknown node type).</exception>
    public static Diagram Load(string path, NodeTypeRegistry types)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentNullException.ThrowIfNull(types);

        // The styler is configuration and is not in the file, so every loaded diagram gets the demo options (stylers, rules)
        using var stream = File.OpenRead(path);
        return Diagram.FromJson(stream, types, DemoAppearance.Options);
    }

    /// <summary>
    ///     Saves a diagram as an indented snapshot JSON file, replacing any existing file.
    /// </summary>
    /// <param name="diagram">The diagram to save. Must not be null.</param>
    /// <param name="path">The file to write. Must not be null or empty.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="diagram"/> is null.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="path"/> is null or empty.</exception>
    /// <exception cref="IOException">Thrown when the file cannot be written.</exception>
    public static void Save(Diagram diagram, string path)
    {
        ArgumentNullException.ThrowIfNull(diagram);
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        using var stream = File.Create(path);
        diagram.ToJson(stream);
    }
}
