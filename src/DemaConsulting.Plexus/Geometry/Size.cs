namespace DemaConsulting.Plexus.Geometry;

/// <summary>
///     An immutable width and height pair in diagram coordinates.
/// </summary>
/// <remarks>
///     Kept separate from <see cref="Point"/> so APIs state clearly whether they accept a
///     location or an extent.
/// </remarks>
/// <param name="Width">The horizontal extent.</param>
/// <param name="Height">The vertical extent.</param>
public readonly record struct Size(double Width, double Height)
{
    /// <summary>
    ///     Gets the empty size (0 x 0).
    /// </summary>
    public static Size Empty => new(0, 0);
}
