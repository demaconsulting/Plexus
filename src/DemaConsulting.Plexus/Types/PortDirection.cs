namespace DemaConsulting.Plexus;

/// <summary>
///     Whether a port receives data (input, drawn on the left) or produces it (output, drawn on the right).
/// </summary>
public enum PortDirection
{
    /// <summary>The port consumes data; it is the target end of a wire and sits on the left edge.</summary>
    Input,

    /// <summary>The port produces data; it is the source end of a wire and sits on the right edge.</summary>
    Output,
}
