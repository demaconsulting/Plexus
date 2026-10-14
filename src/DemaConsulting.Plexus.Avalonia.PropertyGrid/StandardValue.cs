namespace DemaConsulting.Plexus.Avalonia.PropertyGrid;

/// <summary>
///     One entry of the list of standard values a property offers (enumeration members, <see cref="bool"/> values or the
///     values listed by a type converter).
/// </summary>
/// <param name="Value">The value assigned to the property when the entry is chosen.</param>
/// <param name="Text">The text shown for the entry, produced by the property's type converter.</param>
public sealed record StandardValue(object? Value, string Text);
