namespace DemaConsulting.Plexus.Demo.Model;

/// <summary>
///     The symbol a <see cref="GateVisual"/> draws.
/// </summary>
public enum GateShape
{
    /// <summary>The AND gate: a flat back and a rounded front.</summary>
    And,

    /// <summary>The OR gate: a concave back and a pointed front.</summary>
    Or,

    /// <summary>The exclusive-OR gate: the OR shape with an extra curve behind it.</summary>
    Xor,

    /// <summary>The NOT gate: a triangle ending in an inversion bubble.</summary>
    Not,
}
