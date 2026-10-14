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
///     // A source over values the host keeps itself, without reflection
///     public sealed class SettingsSource : IPropertySource
///     {
///         private readonly CustomPropertySource _inner = new(
///         [
///             new CustomPropertyItem("Volume", typeof(int), () => volume, v => volume = (int)v!) { Category = "Audio" },
///         ]);
///         private int volume = 5;
///
///         public IReadOnlyList<PropertyItem> GetItems() => _inner.GetItems();
///         public event EventHandler<PropertySourceChangedEventArgs>? Changed { add { _inner.Changed += value; } remove { _inner.Changed -= value; } }
///     }
///     ]]></code>
/// </example>
public interface IPropertySource
{
    /// <summary>
    ///     Gets the browsable items of the source (non-browsable items are omitted unless the source was asked to include
    ///     them).
    /// </summary>
    /// <returns>The items, unsorted.</returns>
    IReadOnlyList<PropertyItem> GetItems();

    /// <summary>
    ///     Occurs when the values (or the set) of the items may have changed.
    /// </summary>
    event EventHandler<PropertySourceChangedEventArgs>? Changed;
}
