using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Media.Imaging;
using Avalonia.Platform;

namespace DemaConsulting.Plexus.Avalonia.Tests;

/// <summary>
///     The pixels of a captured frame, row by row, as RGB triples.
/// </summary>
/// <param name="Pixels">The pixels.</param>
/// <param name="Width">The width in pixels.</param>
/// <param name="Height">The height in pixels.</param>
internal sealed record Picture((byte R, byte G, byte B)[] Pixels, int Width, int Height)
{
    /// <summary>
    ///     Gets the pixel at a position.
    /// </summary>
    /// <param name="x">The column.</param>
    /// <param name="y">The row.</param>
    /// <returns>The pixel.</returns>
    public (byte R, byte G, byte B) At(int x, int y) => Pixels[(y * Width) + x];
}

/// <summary>
///     Reads the pixels of captured frames.
/// </summary>
internal static class PictureReader
{
    /// <summary>
    ///     Reads every pixel of a bitmap.
    /// </summary>
    /// <param name="bitmap">The bitmap.</param>
    /// <returns>The pixels.</returns>
    public static Picture Read(WriteableBitmap bitmap)
    {
        ArgumentNullException.ThrowIfNull(bitmap);

        // Saving and reloading through PNG gives a predictable pixel layout regardless of the platform format
        using var stream = new MemoryStream();
        bitmap.Save(stream, PngBitmapEncoderOptions.Default);
        stream.Position = 0;
        using var decoded = new Bitmap(stream);
        var size = decoded.PixelSize;
        var buffer = new byte[size.Width * size.Height * 4];
        var handle = GCHandle.Alloc(buffer, GCHandleType.Pinned);
        try
        {
            decoded.CopyPixels(new PixelRect(0, 0, size.Width, size.Height), handle.AddrOfPinnedObject(), buffer.Length, size.Width * 4);
        }
        finally
        {
            handle.Free();
        }

        // The decoded bitmap uses the platform's native layout: BGRA on most platforms, RGBA on macOS arm64
        var rgba = decoded.Format == PixelFormats.Rgba8888;
        var (red, blue) = rgba ? (0, 2) : (2, 0);
        var result = new (byte R, byte G, byte B)[size.Width * size.Height];
        for (var i = 0; i < result.Length; i++)
        {
            result[i] = (buffer[(i * 4) + red], buffer[(i * 4) + 1], buffer[(i * 4) + blue]);
        }

        return new Picture(result, size.Width, size.Height);
    }
}
