namespace DemaConsulting.Plexus;

/// <content>
///     Snapshot support: loading values into a bag whose node is not attached to a page yet.
/// </content>
public sealed partial class PropertyBag
{
    /// <summary>
    ///     Stores a value without validation, history or notifications.
    /// </summary>
    /// <remarks>
    ///     Used while a node is being materialized from a snapshot or clipboard payload, before it is
    ///     attached to a page (the public setter requires an attached node). The caller must already
    ///     have coerced <paramref name="canonical"/> to the declared kind; names that are not declared
    ///     become ad-hoc values appended in call order.
    /// </remarks>
    /// <param name="name">The property name.</param>
    /// <param name="canonical">The canonical value (bool, int, long, double, string or Color).</param>
    internal void LoadValue(string name, object canonical)
    {
        // Declared names already have a slot; anything else is ad-hoc and keeps insertion order
        if (!_values.ContainsKey(name))
        {
            _adHocOrder.Add(name);
        }

        _values[name] = canonical;
    }
}
