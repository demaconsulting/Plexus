namespace DemaConsulting.Plexus.Avalonia.PropertyGrid;

/// <summary>
///     Supplies the property items a grid shows. This is the grid's only input abstraction: implement it to supply
///     items without reflection (see also <see cref="CustomPropertySource"/> and <see cref="CustomPropertyItem"/>),
///     or use <see cref="ReflectionPropertySource"/> for annotated .NET objects and
///     <see cref="CompositePropertySource"/> to merge several sources.
/// </summary>
/// <remarks>
///     <para>
///         <see cref="GetItems"/> returns the same <see cref="PropertyItem"/> instances until the source changes, so a
///         user interface can keep per-row state. Items are returned unsorted; use <see cref="PropertyOrdering"/> to
///         order and group them.
///     </para>
///     <para>
///         Implementations that wrap an object that can change by itself raise <see cref="Changed"/> so the grid
///         re-reads values. The event may be raised on any thread; a user interface must marshal to its own thread.
///     </para>
/// </remarks>
/// <example>
///     <code><![CDATA[
///     using DemaConsulting.Plexus.Avalonia.PropertyGrid;
///
///     // A source over values the host keeps itself, without reflection
///     public sealed class SettingsSource : IPropertySource
///     {
///         private readonly CustomPropertySource _inner;
///         private int _volume = 5;
///
///         public SettingsSource()
///         {
///             _inner = new CustomPropertySource(
///             [
///                 new CustomPropertyItem("Volume", typeof(int), () => _volume, v => _volume = (int)v!, new CustomPropertyOptions { Category = "Audio" }),
///             ]);
///         }
///
///         public IReadOnlyList<PropertyItem> GetItems() => _inner.GetItems();
///         public event EventHandler<PropertySourceChangedEventArgs>? Changed { add { _inner.Changed += value; } remove { _inner.Changed -= value; } }
///     }
///     ]]></code>
/// </example>
public interface IPropertySource
{
    /// <summary>
    ///     Gets the items the grid shows; a source leaves out the items that must not be shown (for example non-browsable properties).
    /// </summary>
    /// <returns>The items, unsorted.</returns>
    IReadOnlyList<PropertyItem> GetItems();

    /// <summary>
    ///     Occurs when the values (or the set) of the items may have changed.
    /// </summary>
    event EventHandler<PropertySourceChangedEventArgs>? Changed;
}
