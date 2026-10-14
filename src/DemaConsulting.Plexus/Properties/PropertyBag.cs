using System.Collections;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;

namespace DemaConsulting.Plexus;

/// <summary>
///     The named values of a <see cref="Node"/>: declared properties (with defaults) plus ad-hoc values.
/// </summary>
/// <remarks>
///     Declared properties come from <see cref="NodeType.Properties"/>, start at their defaults and
///     are validated against their kind (and choices). Undeclared names are allowed as ad-hoc values
///     as long as the value is one of the portable kinds (bool, int, long, double, string, Color).
///     Compatible numeric types convert, so assigning an <see cref="int"/> to a double property works.
///     Every assignment is undoable and a no-op assignment records nothing. Enumeration lists declared
///     properties first (in declaration order) and then ad-hoc values (in insertion order).
///     When the node's type declares a <see cref="NodeType.PortProvider"/>, every change (assignment, <see cref="Reset"/>,
///     <see cref="TrySetAll"/>, ad-hoc values) also recomputes the node's ports inside the same undo entry; see
///     <see cref="NodeType.PortProvider"/> for how wires are kept or removed.
///     Not thread-safe.
/// </remarks>
/// <example>
///     Read and write a node's values, and group several edits into one undo step.
///     <code>
///     using DemaConsulting.Plexus;
///
///     var diagram = new Diagram(types);                     // types: the registry of the DemaConsulting.Plexus example
///     var a = diagram.Pages.Add("Main").AddNode("demo.Source", 0, 0, name: "A");
///     var props = a.Properties;
///     double value = props.Get&lt;double&gt;("Value");             // typed read; numeric kinds convert when no precision is lost
///     props["Value"] = 7;                                   // validated and undoable; an int converts to the double property
///     props["Comment"] = "hand-tuned";                      // undeclared names are allowed as ad-hoc values
///
///     if (props.TryGetValue("Comment", out var comment))
///     {
///         Console.WriteLine(comment);
///     }
///
///     // Several edits as ONE undo entry: wrap them in Transact (it also rolls everything back if one of them throws)
///     diagram.History.Transact("Tune source", () =>
///     {
///         props["Value"] = 9;
///         props["Comment"] = "final";
///     });
///
///     diagram.History.Undo();                               // reverts both edits at once
///     props.Reset("Value");                                 // back to the declared default (undoable)
///     foreach (var (name, current) in props)                // declared properties first, then ad-hoc values
///     {
///         Console.WriteLine($"{name} = {current}");
///     }
///     </code>
/// </example>
public sealed partial class PropertyBag : IReadOnlyDictionary<string, object>, INotifyPropertyChanged
{
    /// <summary>
    ///     The property name reported alongside every value change, matching the indexer convention of INotifyPropertyChanged.
    /// </summary>
    private const string IndexerName = "Item[]";

    /// <summary>
    ///     The node the bag belongs to.
    /// </summary>
    private readonly Node _owner;

    /// <summary>
    ///     Current values of declared and ad-hoc properties.
    /// </summary>
    private readonly Dictionary<string, object> _values = new(StringComparer.Ordinal);

    /// <summary>
    ///     Names of ad-hoc values in insertion order.
    /// </summary>
    private readonly List<string> _adHocOrder = [];

    /// <summary>
    ///     Initializes a new instance of the <see cref="PropertyBag"/> class holding the node type's default values.
    /// </summary>
    /// <param name="owner">The node the bag belongs to.</param>
    internal PropertyBag(Node owner)
    {
        _owner = owner;
        foreach (var definition in owner.Type.Properties)
        {
            _values[definition.Name] = definition.DefaultValue;
        }
    }

    /// <summary>
    ///     Occurs after a property value changed (assignment, <see cref="Reset"/>, undo or redo).
    /// </summary>
    /// <remarks>
    ///     Raised twice per change: once with the property's name and once with <c>"Item[]"</c>, the name data-binding uses for
    ///     indexer changes, so a binding to <c>Properties[Gain]</c> refreshes. The sender is the bag.
    /// </remarks>
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>
    ///     Gets the number of declared and ad-hoc properties currently in the bag.
    /// </summary>
    public int Count => _values.Count;

    /// <summary>
    ///     Gets the names of all declared and ad-hoc properties, declared properties first.
    /// </summary>
    public IEnumerable<string> Keys => Enumerate().Select(p => p.Key);

    /// <summary>
    ///     Gets the current values of all declared and ad-hoc properties, in the same order as <see cref="Keys"/>.
    /// </summary>
    public IEnumerable<object> Values => Enumerate().Select(p => p.Value);

    /// <summary>
    ///     Gets or sets a property value; assignment validates, converts and is undoable.
    /// </summary>
    /// <param name="name">The property name.</param>
    /// <exception cref="KeyNotFoundException">Thrown on read when the name is neither declared nor set.</exception>
    /// <exception cref="ArgumentException">Thrown on write when the value is not allowed for the property.</exception>
    /// <exception cref="InvalidOperationException">Thrown on write when the node is removed from its page.</exception>
    public object this[string name]
    {
        get
        {
            ArgumentNullException.ThrowIfNull(name);
            return _values.TryGetValue(name, out var value)
                ? value
                : throw new KeyNotFoundException($"The node has no property named '{name}'.");
        }

        set => Set(name, value);
    }

    /// <summary>
    ///     Gets a property value converted to the requested type; call it with an explicit type argument, for example <c>Get&lt;double&gt;("Gain")</c>.
    /// </summary>
    /// <typeparam name="T">The wanted type; numeric properties convert when no precision is lost.</typeparam>
    /// <param name="name">The property name.</param>
    /// <returns>The converted value.</returns>
    /// <exception cref="KeyNotFoundException">Thrown when the property does not exist.</exception>
    /// <exception cref="InvalidCastException">Thrown when the value cannot be converted to <typeparamref name="T"/>.</exception>
    public T Get<T>(string name)
    {
        var value = this[name];
        if (value is T typed)
        {
            return typed;
        }

        // Allow numeric widening/narrowing between the primitive kinds, e.g. an int property read as double
        var target = Nullable.GetUnderlyingType(typeof(T)) ?? typeof(T);
        if (value is int or long or double && target.IsPrimitive && target != typeof(bool) && target != typeof(char))
        {
            try
            {
                return (T)Convert.ChangeType(value, target, CultureInfo.InvariantCulture);
            }
            catch (OverflowException ex)
            {
                throw new InvalidCastException($"Property '{name}' value {value} does not fit in {target.Name}.", ex);
            }
        }

        throw new InvalidCastException($"Property '{name}' holds a {value.GetType().Name} and cannot be read as {typeof(T).Name}.");
    }

    /// <summary>
    ///     Finds the declaration of a property.
    /// </summary>
    /// <param name="name">The property name.</param>
    /// <returns>The definition, or null for ad-hoc (undeclared) properties.</returns>
    public PropertyDefinition? GetDefinition(string name) => _owner.Type.FindProperty(name);

    /// <summary>
    ///     Determines whether a property with the given name is declared or has been set.
    /// </summary>
    /// <param name="name">The property name.</param>
    /// <returns><see langword="true"/> when the name is present; otherwise <see langword="false"/>.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="name"/> is null.</exception>
    public bool ContainsKey(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        return _values.ContainsKey(name);
    }

    /// <summary>
    ///     Gets the value of a property without throwing when it is absent.
    /// </summary>
    /// <param name="name">The property name.</param>
    /// <param name="value">When this method returns <see langword="true"/>, the property value.</param>
    /// <returns><see langword="true"/> when the name is present; otherwise <see langword="false"/>.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="name"/> is null.</exception>
    public bool TryGetValue(string name, [MaybeNullWhen(false)] out object value)
    {
        ArgumentNullException.ThrowIfNull(name);
        return _values.TryGetValue(name, out value);
    }

    /// <summary>
    ///     Restores a property to its default (undoable): declared properties return to their default value, ad-hoc properties are removed.
    /// </summary>
    /// <param name="name">The property name.</param>
    /// <exception cref="KeyNotFoundException">Thrown when the property does not exist.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the node is removed from its page.</exception>
    public void Reset(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        _owner.RequirePage();
        if (!_values.TryGetValue(name, out var current))
        {
            throw new KeyNotFoundException($"The node has no property named '{name}'.");
        }

        var definition = GetDefinition(name);
        if (definition is not null)
        {
            // Resetting an already-default value must not create a history entry
            if (!Equals(current, definition.DefaultValue))
            {
                ExecuteSet(name, current, definition.DefaultValue);
            }

            return;
        }

        RemoveAdHoc(name, current);
    }

    /// <summary>
    ///     Returns an enumerator over the name/value pairs: declared properties in declaration order, then ad-hoc values in insertion order.
    /// </summary>
    /// <returns>An enumerator; the bag must not change while enumerating.</returns>
    public IEnumerator<KeyValuePair<string, object>> GetEnumerator() => Enumerate().GetEnumerator();

    /// <inheritdoc />
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    /// <summary>
    ///     Enumerates declared properties in declaration order, then ad-hoc values in insertion order.
    /// </summary>
    /// <returns>The name/value pairs.</returns>
    private IEnumerable<KeyValuePair<string, object>> Enumerate()
    {
        foreach (var name in _owner.Type.Properties.Select(d => d.Name))
        {
            yield return new KeyValuePair<string, object>(name, _values[name]);
        }

        foreach (var name in _adHocOrder)
        {
            yield return new KeyValuePair<string, object>(name, _values[name]);
        }
    }

    /// <summary>
    ///     Validates and applies an assignment.
    /// </summary>
    /// <param name="name">The property name.</param>
    /// <param name="value">The new value.</param>
    private void Set(string name, object value)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);
        ArgumentNullException.ThrowIfNull(value);
        _owner.RequirePage();

        // Declared properties coerce to their kind; ad-hoc ones only need to be an allowed kind
        var definition = GetDefinition(name);
        var canonical = definition is null
            ? PropertyValues.Normalize(value, out _)
            : PropertyValues.Coerce(value, definition.ValueKind, definition.Choices);

        // An unchanged value is a no-op and must not create a history entry
        if (_values.TryGetValue(name, out var current))
        {
            if (!Equals(current, canonical))
            {
                ExecuteSet(name, current, canonical);
            }

            return;
        }

        AddAdHoc(name, canonical);
    }

    /// <summary>
    ///     Records an undoable change of an existing value, recomputing the node's ports in the same entry when its type has a port provider.
    /// </summary>
    /// <param name="name">The property name.</param>
    /// <param name="oldValue">The current value.</param>
    /// <param name="newValue">The new canonical value.</param>
    private void ExecuteSet(string name, object oldValue, object newValue) =>
        _owner.ExecuteAffectingPorts(new SetValueCommand<object>(
            $"Set {name}",
            this,
            name,
            oldValue,
            newValue,
            v => ApplyValue(name, v)));

    /// <summary>
    ///     Records the undoable creation of an ad-hoc value, recomputing the node's ports in the same entry when its type has a port provider.
    /// </summary>
    /// <param name="name">The new property name.</param>
    /// <param name="value">The canonical value.</param>
    private void AddAdHoc(string name, object value) =>
        _owner.ExecuteAffectingPorts(new DelegateCommand(
            $"Add {name}",
            () =>
            {
                _values[name] = value;
                _adHocOrder.Add(name);
                RaiseChanged(name);
            },
            () =>
            {
                _values.Remove(name);
                _adHocOrder.Remove(name);
                RaiseChanged(name);
            }));

    /// <summary>
    ///     Records the undoable removal of an ad-hoc value, remembering its position and recomputing the node's ports in the same entry when its type has a port provider.
    /// </summary>
    /// <param name="name">The property name.</param>
    /// <param name="value">The current value.</param>
    private void RemoveAdHoc(string name, object value)
    {
        var index = _adHocOrder.IndexOf(name);
        _owner.ExecuteAffectingPorts(new DelegateCommand(
            $"Reset {name}",
            () =>
            {
                _values.Remove(name);
                _adHocOrder.Remove(name);
                RaiseChanged(name);
            },
            () =>
            {
                _values[name] = value;
                _adHocOrder.Insert(index, name);
                RaiseChanged(name);
            }));
    }

    /// <summary>
    ///     Stores a value and raises notifications; used by do, undo and redo of value changes.
    /// </summary>
    /// <param name="name">The property name.</param>
    /// <param name="value">The value to store.</param>
    private void ApplyValue(string name, object value)
    {
        _values[name] = value;
        RaiseChanged(name);
    }

    /// <summary>
    ///     Raises the bag's <see cref="PropertyChanged"/> events and reports the change to the node and diagram.
    /// </summary>
    /// <param name="name">The changed property name.</param>
    private void RaiseChanged(string name)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(IndexerName));
        _owner.NotifyChanged(nameof(Node.Properties));
    }
}
