namespace DemaConsulting.Plexus;

/// <content>
///     Editing support: applying several edited values as one undo entry.
/// </content>
public sealed partial class PropertyBag
{
    /// <summary>
    ///     Applies several property changes all-or-nothing: either every change is valid and they are applied as ONE undo entry, or none is applied.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         Every entry is validated first (declared properties against their kind and choices, undeclared names as
    ///         portable ad-hoc values). A value that is a <see cref="string"/> for a property that is not a string property is
    ///         read with <see cref="PropertyDefinition.TryParse"/>, so the raw text of an edit box can be passed as it is
    ///         and a mistake comes back as a message such as <c>'abc' is not a number.</c>. Values that equal the current value record
    ///         nothing; when nothing changes at all no undo entry is created. A null value is an error (use <see cref="Reset"/> to
    ///         restore a default). <see cref="PropertyDefinition.IsReadOnly"/> is a hint and is not enforced here.
    ///     </para>
    ///     <para>
    ///         To change host data (<see cref="Node.Data"/>) in the same undo entry, wrap both in <see cref="UndoHistory.Transact(string, Action)"/>
    ///         and assign the data inside it.
    ///     </para>
    ///     <para>
    ///         When the node's type declares a <see cref="NodeType.PortProvider"/>, the ports are recomputed after each applied
    ///         value, still inside the single undo entry. If the provider throws, every change is rolled back and the exception propagates.
    ///     </para>
    /// </remarks>
    /// <example>
    ///     <code>
    ///     var texts = new Dictionary&lt;string, object?&gt; { ["Gain"] = "2.5", ["Mode"] = "Slow", ["Label"] = "Main" };
    ///     if (!node.Properties.TrySetAll(texts, out var errors))
    ///     {
    ///         foreach (var (name, message) in errors)
    ///         {
    ///             Console.WriteLine($"{name}: {message}");       // nothing was changed
    ///         }
    ///     }
    ///     </code>
    /// </example>
    /// <param name="changes">The new values by property name.</param>
    /// <param name="errors">Receives a message per rejected property (empty on success).</param>
    /// <returns><see langword="true"/> when all changes were valid and applied; <see langword="false"/> when any was rejected and nothing changed.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="changes"/> is null.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the node is removed from its page.</exception>
    public bool TrySetAll(IReadOnlyDictionary<string, object?> changes, out IReadOnlyDictionary<string, string> errors)
    {
        ArgumentNullException.ThrowIfNull(changes);
        var page = _owner.RequirePage();

        // Validate everything before touching anything, so a rejection leaves no partial edit behind
        var failures = new Dictionary<string, string>(StringComparer.Ordinal);
        var accepted = new List<(string Name, object Value)>();
        foreach (var (name, requested) in changes)
        {
            if (TryResolve(name, requested, out var canonical, out var problem))
            {
                accepted.Add((name, canonical!));
            }
            else
            {
                failures[name ?? string.Empty] = problem!;
            }
        }

        errors = failures;
        if (failures.Count > 0)
        {
            return false;
        }

        page.Diagram.History.Transact("Edit properties", () =>
        {
            foreach (var (name, value) in accepted)
            {
                Set(name, value);
            }
        });
        return true;
    }

    /// <summary>
    ///     Validates one requested value and reduces it to its canonical form.
    /// </summary>
    /// <param name="name">The property name.</param>
    /// <param name="requested">The requested value (or text).</param>
    /// <param name="canonical">Receives the canonical value on success.</param>
    /// <param name="problem">Receives the reason on failure.</param>
    /// <returns><see langword="true"/> when the value is acceptable.</returns>
    private bool TryResolve(string name, object? requested, out object? canonical, out string? problem)
    {
        canonical = null;
        problem = null;
        if (string.IsNullOrEmpty(name))
        {
            problem = "A property name is required.";
            return false;
        }

        if (requested is null)
        {
            problem = "A value is required.";
            return false;
        }

        var definition = GetDefinition(name);

        // Text for a typed property is parsed, so hosts can hand over what the user typed
        if (requested is string text && definition is { ValueKind: not PropertyValueKind.String })
        {
            return definition.TryParse(text, out canonical, out problem);
        }

        try
        {
            canonical = definition is null
                ? PropertyValues.Normalize(requested, out _)
                : PropertyValues.Coerce(requested, definition.ValueKind, definition.Choices);
            return true;
        }
        catch (ArgumentException ex)
        {
            // The framework appends the parameter name, which means nothing to the user
            problem = ex.Message.Replace(" (Parameter 'value')", string.Empty, StringComparison.Ordinal);
            return false;
        }
    }
}
