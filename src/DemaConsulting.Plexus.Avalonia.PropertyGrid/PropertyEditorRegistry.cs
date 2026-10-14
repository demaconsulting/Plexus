using System.Collections.Concurrent;

namespace DemaConsulting.Plexus.Avalonia.PropertyGrid;

/// <summary>
///     Maps editor keys and property types to the editors a grid uses, so the UI layer (and hosts) can plug custom
///     editors without the generic layers knowing any UI type.
/// </summary>
/// <typeparam name="TEditor">
///     The editor representation chosen by the UI layer (for example an editor factory); the registry only stores and
///     returns it.
/// </typeparam>
/// <remarks>
///     <para>
///         Resolution for an item (<see cref="Resolve(PropertyItem)"/>): first the key from
///         <see cref="PropertyItem.EditorKey"/> (a <see cref="PropertyEditorAttribute"/> or a <c>[UIHint]</c> on the
///         property, else a <see cref="PropertyEditorAttribute"/> on its
///         type), then the property type, then <see cref="Fallback"/>. By type, the registry looks for the exact
///         type, the underlying type of a <see cref="Nullable{T}"/>, each base class from nearest to
///         <see cref="object"/> (so a registration for <see cref="Enum"/> serves every enumeration), then each
///         implemented interface, and finally the generic type definition of each of those (so
///         <c>typeof(List&lt;&gt;)</c> serves every <c>List&lt;T&gt;</c>).
///     </para>
///     <para>
///         WinForms <c>UITypeEditor</c> classes cannot be reused here (see <see cref="PropertyEditorAttribute"/>).
///         The registry is thread-safe.
///     </para>
/// </remarks>
/// <example>
///     <code><![CDATA[
///     var registry = new PropertyEditorRegistry<Func<PropertyItem, Control>>();
///     registry.Register("color-swatch", item => new ColorSwatchEditor(item));
///     registry.Register(typeof(Enum), item => new EnumDropDownEditor(item));
///     registry.Fallback = item => new TextEditor(item);
///
///     Func<PropertyItem, Control>? create = registry.Resolve(someItem);
///     ]]></code>
/// </example>
public sealed class PropertyEditorRegistry<TEditor>
    where TEditor : class
{
    /// <summary>
    ///     The editors by key.
    /// </summary>
    private readonly ConcurrentDictionary<string, TEditor> _byKey = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    ///     The editors by property type.
    /// </summary>
    private readonly ConcurrentDictionary<Type, TEditor> _byType = new();

    /// <summary>
    ///     Gets or sets the editor used when nothing else matches; <see langword="null"/> means none.
    /// </summary>
    public TEditor? Fallback { get; set; }

    /// <summary>
    ///     Registers an editor under a key (replacing an earlier registration of the same key).
    /// </summary>
    /// <param name="key">The key, matched case-insensitively.</param>
    /// <param name="editor">The editor.</param>
    /// <exception cref="ArgumentException"><paramref name="key"/> is <see langword="null"/> or white space.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="editor"/> is <see langword="null"/>.</exception>
    public void Register(string key, TEditor editor)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentNullException.ThrowIfNull(editor);
        _byKey[key] = editor;
    }

    /// <summary>
    ///     Registers an editor for a property type (replacing an earlier registration of the same type).
    /// </summary>
    /// <param name="propertyType">The property type, a base type, an interface or an open generic type definition.</param>
    /// <param name="editor">The editor.</param>
    /// <exception cref="ArgumentNullException"><paramref name="propertyType"/> or <paramref name="editor"/> is <see langword="null"/>.</exception>
    public void Register(Type propertyType, TEditor editor)
    {
        ArgumentNullException.ThrowIfNull(propertyType);
        ArgumentNullException.ThrowIfNull(editor);
        _byType[propertyType] = editor;
    }

    /// <summary>
    ///     Removes the registration of a key.
    /// </summary>
    /// <param name="key">The key.</param>
    /// <returns><see langword="true"/> when a registration was removed.</returns>
    public bool Unregister(string key) => key is not null && _byKey.TryRemove(key, out _);

    /// <summary>
    ///     Removes the registration of a property type.
    /// </summary>
    /// <param name="propertyType">The property type.</param>
    /// <returns><see langword="true"/> when a registration was removed.</returns>
    public bool Unregister(Type propertyType) => propertyType is not null && _byType.TryRemove(propertyType, out _);

    /// <summary>
    ///     Finds the editor registered under a key.
    /// </summary>
    /// <param name="key">The key.</param>
    /// <returns>The editor, or <see langword="null"/> when the key is not registered (<see cref="Fallback"/> is not used).</returns>
    public TEditor? Resolve(string key) => key is not null && _byKey.TryGetValue(key, out var editor) ? editor : null;

    /// <summary>
    ///     Finds the editor for a property type.
    /// </summary>
    /// <param name="propertyType">The property type.</param>
    /// <returns>The editor, or <see langword="null"/> when no registration matches (<see cref="Fallback"/> is not used).</returns>
    /// <exception cref="ArgumentNullException"><paramref name="propertyType"/> is <see langword="null"/>.</exception>
    public TEditor? Resolve(Type propertyType)
    {
        ArgumentNullException.ThrowIfNull(propertyType);
        if (FindByType(propertyType) is { } found)
        {
            return found;
        }

        var underlying = Nullable.GetUnderlyingType(propertyType);
        return underlying is null ? null : FindByType(underlying);
    }

    /// <summary>
    ///     Finds the editor for an item: its key, then its property type, then <see cref="Fallback"/>.
    /// </summary>
    /// <param name="item">The item.</param>
    /// <returns>The editor, or <see langword="null"/> when nothing matches and there is no fallback.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="item"/> is <see langword="null"/>.</exception>
    public TEditor? Resolve(PropertyItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        if (item.EditorKey is { } key && Resolve(key) is { } byKey)
        {
            return byKey;
        }

        return Resolve(item.PropertyType) ?? Fallback;
    }

    /// <summary>
    ///     Walks the type, its bases, its interfaces and their generic definitions looking for a registration.
    /// </summary>
    /// <param name="type">The type.</param>
    /// <returns>The editor, or <see langword="null"/>.</returns>
    private TEditor? FindByType(Type type)
    {
        for (var current = type; current is not null; current = current.BaseType)
        {
            if (_byType.TryGetValue(current, out var exact))
            {
                return exact;
            }
        }

        foreach (var contract in type.GetInterfaces())
        {
            if (_byType.TryGetValue(contract, out var viaInterface))
            {
                return viaInterface;
            }
        }

        for (var current = type; current is not null; current = current.BaseType)
        {
            if (current.IsGenericType && _byType.TryGetValue(current.GetGenericTypeDefinition(), out var viaDefinition))
            {
                return viaDefinition;
            }
        }

        foreach (var contract in type.GetInterfaces())
        {
            if (contract.IsGenericType && _byType.TryGetValue(contract.GetGenericTypeDefinition(), out var viaOpenInterface))
            {
                return viaOpenInterface;
            }
        }

        return null;
    }
}
