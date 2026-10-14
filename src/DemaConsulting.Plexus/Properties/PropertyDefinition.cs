using System.Globalization;

namespace DemaConsulting.Plexus;

/// <summary>
///     Declares an editable property of a <see cref="NodeType"/>: its name, kind, default and presentation hints.
/// </summary>
/// <remarks>
///     The value kind is inferred from the default value, which keeps declarations short:
///     <c>new PropertyDefinition("Gain", 1.0)</c> is a double (note <c>1.0</c> versus <c>1</c>, which would be an int).
///     There is no separate "choice" constructor: set <see cref="Choices"/> on a
///     string property to constrain it to a list, for example
///     <c>new PropertyDefinition("Mode", "Fast") { Choices = ["Fast", "Slow"], ShowInSummary = true }</c>; its
///     <see cref="ValueKind"/> then reads <see cref="PropertyValueKind.Choice"/>. Assigning a value that is not one of the
///     choices (or any value of the wrong kind) through <see cref="PropertyBag"/> throws <see cref="ArgumentException"/>.
///     Presentation members (<see cref="DisplayName"/>, <see cref="Category"/>,
///     <see cref="Unit"/>, <see cref="EditorHint"/>, <see cref="IsReadOnly"/>) are hints for UIs and
///     summaries; they never restrict programmatic changes through <see cref="PropertyBag"/>. All members other than
///     <see cref="Name"/>, <see cref="ValueKind"/> and <see cref="DefaultValue"/> are set with an object initializer.
/// </remarks>
/// <example>
///     The kind comes from the default value: a double, an int, a bool, a string or a <see cref="Color"/>.
///     <code>
///     using DemaConsulting.Plexus;
///     using DemaConsulting.Plexus.Geometry;
///
///     var gain = new PropertyDefinition("Gain", 1.0) { Unit = "dB", Category = "Audio", ShowInSummary = true };
///     var channels = new PropertyDefinition("Channels", 2);                   // int
///     var enabled = new PropertyDefinition("Enabled", true);                  // bool
///     var tint = new PropertyDefinition("Tint", Color.Orange);                // Color
///     var mode = new PropertyDefinition("Mode", "Fast")
///     {
///         Choices = ["Fast", "Slow", "Off"],                // restricts a string property to these values; the default must be one of them
///         Description = "How quickly the filter reacts.",
///     };
///
///     var types = new NodeTypeRegistry();
///     types.Add(new NodeType("audio.Filter") { Properties = { gain, channels, enabled, tint, mode } });
///
///     var diagram = new Diagram(types);
///     var node = diagram.Pages.Add("Main").AddNode("audio.Filter", 0, 0);
///     Console.WriteLine(mode.ValueKind);                    // Choice
///     node.Properties["Mode"] = "Slow";                     // fine
///     try
///     {
///         node.Properties["Mode"] = "Medium";               // not a choice
///     }
///     catch (ArgumentException ex)
///     {
///         Console.WriteLine(ex.Message);                    // 'Medium' is not one of the allowed choices (Fast, Slow, Off).
///     }
///     </code>
/// </example>
public sealed class PropertyDefinition
{
    /// <summary>
    ///     Optional explicit display name; null means the name is used.
    /// </summary>
    private string? _displayName;

    /// <summary>
    ///     The allowed values of a choice property, or null.
    /// </summary>
    private IReadOnlyList<string>? _choices;

    /// <summary>
    ///     Initializes a new instance of the <see cref="PropertyDefinition"/> class, inferring the kind from the default.
    /// </summary>
    /// <param name="name">The property name, unique within the node type.</param>
    /// <param name="defaultValue">The default value; its type determines <see cref="ValueKind"/>.</param>
    /// <exception cref="ArgumentException">Thrown when the name is blank or the default value is not an allowed kind.</exception>
    public PropertyDefinition(string name, object defaultValue)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        Name = name;
        DefaultValue = PropertyValues.Normalize(defaultValue, out var kind);
        ValueKind = kind;
    }

    /// <summary>
    ///     Gets the property name.
    /// </summary>
    public string Name { get; }

    /// <summary>
    ///     Gets the kind of value the property holds; <see cref="PropertyValueKind.Choice"/> once <see cref="Choices"/> is set.
    /// </summary>
    public PropertyValueKind ValueKind { get; private set; }

    /// <summary>
    ///     Gets the default value in canonical form (bool, int, long, double, string or Color).
    /// </summary>
    public object DefaultValue { get; private set; }

    /// <summary>
    ///     Gets or initializes the allowed values of a choice property; null (the default) means the property is not constrained to a list.
    /// </summary>
    /// <remarks>
    ///     Setting it (in an object initializer) turns a string property into a
    ///     <see cref="PropertyValueKind.Choice"/> property. The list is copied.
    /// </remarks>
    /// <exception cref="ArgumentException">
    ///     Thrown when the property is not a string, the list is empty or has duplicates, or the
    ///     default value is not one of the choices.
    /// </exception>
    public IReadOnlyList<string>? Choices
    {
        get => _choices;
        init
        {
            if (value is null)
            {
                return;
            }

            if (ValueKind != PropertyValueKind.String)
            {
                throw new ArgumentException($"Property \"{Name}\" has choices, so its default value must be a string.", nameof(value));
            }

            if (value.Count == 0 || value.Distinct(StringComparer.Ordinal).Count() != value.Count)
            {
                throw new ArgumentException("A choice property needs at least one choice and the choices must be unique.", nameof(value));
            }

            // The default must be selectable, otherwise a fresh node would already hold an invalid value
            _choices = Array.AsReadOnly([.. value]);
            DefaultValue = PropertyValues.Coerce(DefaultValue, PropertyValueKind.Choice, _choices);
            ValueKind = PropertyValueKind.Choice;
        }
    }

    /// <summary>
    ///     Gets or initializes the label shown in UIs and summaries; defaults to <see cref="Name"/>.
    /// </summary>
    public string DisplayName
    {
        get => _displayName ?? Name;
        init => _displayName = value;
    }

    /// <summary>
    ///     Gets or initializes a longer description for tooltips and help; null (the default) means none. A hint for host UIs only.
    /// </summary>
    public string? Description { get; init; }

    /// <summary>
    ///     Gets or initializes the category used to group properties in editors; null (the default) means uncategorized. A hint for host UIs only.
    /// </summary>
    public string? Category { get; init; }

    /// <summary>
    ///     Gets or initializes the unit appended to the value in node summaries (for example "ms"); null (the default) means none.
    /// </summary>
    public string? Unit { get; init; }

    /// <summary>
    ///     Gets or initializes a value indicating whether the property appears in the node's summary lines (as <c>DisplayName: value unit</c>); the default is false.
    /// </summary>
    public bool ShowInSummary { get; init; }

    /// <summary>
    ///     Gets or initializes a value indicating whether editors should present the property as read-only; the default is false.
    /// </summary>
    /// <remarks>A hint for host UIs: assignments through <see cref="PropertyBag"/> are still accepted.</remarks>
    public bool IsReadOnly { get; init; }

    /// <summary>
    ///     Gets or initializes a free-form hint telling a host's property editor which control to use; null (the default) means no preference.
    /// </summary>
    /// <remarks>
    ///     Plexus defines no hint values and no built-in property editor reads this member: it is carried for your own UI, so choose any
    ///     convention you like (for example "multiline", "slider" or "file") and switch on it when you build the editor for a property.
    ///     Without a hint, pick the control from <see cref="ValueKind"/> (see <see cref="PropertyValueKind"/>).
    /// </remarks>
    public string? EditorHint { get; init; }

    /// <summary>
    ///     Gets or initializes an optional function that formats the value for display in the node summary; null (the default) uses invariant-culture formatting (<c>#RRGGBB</c> for colors).
    /// </summary>
    public Func<object?, string>? Format { get; init; }

    /// <summary>
    ///     Formats a value for display, using <see cref="Format"/> when supplied.
    /// </summary>
    /// <param name="value">The value to format.</param>
    /// <returns>The display text (without the unit).</returns>
    public string FormatValue(object? value) => Format is { } format ? format(value) : PropertyValues.Format(value);

    /// <summary>
    ///     Formats the property as a summary line: <c>DisplayName: value</c> followed by the unit when one is set.
    /// </summary>
    /// <param name="value">The current value.</param>
    /// <returns>The summary line.</returns>
    public string FormatSummary(object? value)
    {
        var text = $"{DisplayName}: {FormatValue(value)}";
        return string.IsNullOrEmpty(Unit) ? text : $"{text} {Unit}";
    }

    /// <summary>
    ///     Formats a value as the text an editor shows in an edit box; <see cref="TryParse"/> turns the text back into the same value.
    /// </summary>
    /// <remarks>
    ///     Unlike <see cref="FormatValue"/>, which is for display and honors <see cref="Format"/> and the unit, this text is
    ///     meant to be edited: invariant culture, no unit, booleans as <c>true</c>/<c>false</c>, doubles with the shortest text that
    ///     reads back exactly and colors as <c>#RRGGBB</c> (or <c>#AARRGGBB</c> when translucent). The name differs from the
    ///     <see cref="Format"/> member because that one is the display callback.
    /// </remarks>
    /// <param name="value">The value to format; null formats as an empty string.</param>
    /// <returns>The text, which parses back to the same value.</returns>
    public static string FormatForEdit(object? value) => PropertyValues.FormatForEdit(value);

    /// <summary>
    ///     Converts the text a user typed into a value of this property's kind, with the reason when it does not fit.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         Parsing is culture-invariant (<c>2.5</c>, never <c>2,5</c>) and ignores surrounding white space (except for
    ///         <see cref="PropertyValueKind.String"/>, which keeps the text exactly). The accepted forms are:
    ///         booleans <c>true</c>/<c>false</c> (also <c>yes</c>/<c>no</c>, <c>on</c>/<c>off</c>, <c>1</c>/<c>0</c>, any case); whole numbers for
    ///         <see cref="PropertyValueKind.Int32"/> and <see cref="PropertyValueKind.Int64"/>; finite numbers for
    ///         <see cref="PropertyValueKind.Double"/> (exponent notation allowed); <c>#RRGGBB</c> or <c>#AARRGGBB</c> for
    ///         <see cref="PropertyValueKind.Color"/> (the <c>#</c> may be omitted); and for <see cref="PropertyValueKind.Choice"/> one of
    ///         <see cref="Choices"/> (exact match first, otherwise a unique match ignoring case, which returns the declared spelling).
    ///         A trailing <see cref="Unit"/> is tolerated on numbers, so <c>"250 ms"</c> parses for a property whose unit is <c>ms</c>.
    ///     </para>
    ///     <para>
    ///         On success the value has the exact CLR type the property stores (<see cref="int"/>, <see cref="long"/>, <see cref="double"/>,
    ///         <see cref="bool"/>, <see cref="string"/> or <see cref="Color"/>), ready for <see cref="PropertyBag"/>. On failure
    ///         <paramref name="error"/> is a sentence for the user. This method does not check <see cref="IsReadOnly"/>, which is only a hint.
    ///     </para>
    /// </remarks>
    /// <example>
    ///     <code>
    ///     var gain = new PropertyDefinition("Gain", 1.0) { Unit = "dB" };
    ///     if (gain.TryParse("2.5 dB", out var value, out var error))
    ///     {
    ///         node.Properties["Gain"] = value!;                 // a double
    ///     }
    ///     else
    ///     {
    ///         Console.WriteLine(error);                         // "'abc' is not a number."
    ///     }
    ///     </code>
    /// </example>
    /// <param name="text">The text to parse; null is treated as empty.</param>
    /// <param name="value">Receives the parsed value, or null on failure.</param>
    /// <param name="error">Receives the reason for a failure, or null on success.</param>
    /// <returns><see langword="true"/> when the text is a valid value for this property.</returns>
    public bool TryParse(string? text, out object? value, out string? error)
    {
        var source = text ?? string.Empty;
        error = null;
        value = null;
        if (ValueKind == PropertyValueKind.String)
        {
            value = source;
            return true;
        }

        var trimmed = StripUnit(source.Trim());
        object? parsed = ValueKind switch
        {
            PropertyValueKind.Boolean => ParseBoolean(trimmed, out error),
            PropertyValueKind.Int32 => ParseInteger(trimmed, int.MinValue, int.MaxValue, "a whole number", out error) is { } i ? (int)i : null,
            PropertyValueKind.Int64 => ParseInteger(trimmed, long.MinValue, long.MaxValue, "a whole number", out error),
            PropertyValueKind.Double => ParseDouble(trimmed, out error),
            PropertyValueKind.Color => ParseColor(trimmed, out error),
            _ => ParseChoice(trimmed, out error),
        };

        value = parsed;
        return parsed is not null;
    }

    /// <summary>
    ///     Removes a trailing unit from numeric text.
    /// </summary>
    /// <param name="text">The trimmed text.</param>
    /// <returns>The text without the unit when this is a numeric property and the text ends with its unit.</returns>
    private string StripUnit(string text)
    {
        var numeric = ValueKind is PropertyValueKind.Int32 or PropertyValueKind.Int64 or PropertyValueKind.Double;
        return numeric && !string.IsNullOrEmpty(Unit) && text.Length > Unit.Length && text.EndsWith(Unit, StringComparison.OrdinalIgnoreCase)
            ? text[..^Unit.Length].TrimEnd()
            : text;
    }

    /// <summary>
    ///     Parses a boolean.
    /// </summary>
    /// <param name="text">The trimmed text.</param>
    /// <param name="error">Receives the reason on failure.</param>
    /// <returns>The boxed value, or null.</returns>
    private static object? ParseBoolean(string text, out string? error)
    {
        error = null;
        switch (text.ToLowerInvariant())
        {
            case "true" or "yes" or "on" or "1":
                return true;
            case "false" or "no" or "off" or "0":
                return false;
            default:
                error = $"'{text}' is not true or false.";
                return null;
        }
    }

    /// <summary>
    ///     Parses a whole number within a range.
    /// </summary>
    /// <param name="text">The trimmed text.</param>
    /// <param name="min">The smallest accepted value.</param>
    /// <param name="max">The largest accepted value.</param>
    /// <param name="what">What the text should be, for the message.</param>
    /// <param name="error">Receives the reason on failure.</param>
    /// <returns>The value as a long, or null.</returns>
    private static long? ParseInteger(string text, long min, long max, string what, out string? error)
    {
        error = null;
        if (long.TryParse(text, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var number))
        {
            if (number >= min && number <= max)
            {
                return number;
            }

            error = $"'{text}' is out of range ({min.ToString(CultureInfo.InvariantCulture)} to {max.ToString(CultureInfo.InvariantCulture)}).";
            return null;
        }

        // Digits that do not fit a long are a range problem rather than a syntax problem
        var digits = text.TrimStart('+', '-');
        error = digits.Length > 0 && digits.All(char.IsAsciiDigit)
            ? $"'{text}' is out of range ({min.ToString(CultureInfo.InvariantCulture)} to {max.ToString(CultureInfo.InvariantCulture)})."
            : $"'{text}' is not {what}.";
        return null;
    }

    /// <summary>
    ///     Parses a finite double.
    /// </summary>
    /// <param name="text">The trimmed text.</param>
    /// <param name="error">Receives the reason on failure.</param>
    /// <returns>The boxed value, or null.</returns>
    private static object? ParseDouble(string text, out string? error)
    {
        error = null;
        if (!double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var number))
        {
            error = $"'{text}' is not a number.";
            return null;
        }

        if (!double.IsFinite(number))
        {
            error = $"'{text}' is not a finite number.";
            return null;
        }

        return number;
    }

    /// <summary>
    ///     Parses a color, tolerating a missing <c>#</c>.
    /// </summary>
    /// <param name="text">The trimmed text.</param>
    /// <param name="error">Receives the reason on failure.</param>
    /// <returns>The boxed color, or null.</returns>
    private static object? ParseColor(string text, out string? error)
    {
        error = null;
        var candidate = text.StartsWith('#') ? text : "#" + text;
        if (Color.TryParse(candidate, out var color))
        {
            return color;
        }

        error = $"'{text}' is not a color; use #RRGGBB or #AARRGGBB.";
        return null;
    }

    /// <summary>
    ///     Parses one of the choices.
    /// </summary>
    /// <param name="text">The trimmed text.</param>
    /// <param name="error">Receives the reason on failure.</param>
    /// <returns>The declared choice, or null.</returns>
    private object? ParseChoice(string text, out string? error)
    {
        error = null;
        var choices = _choices ?? [];
        if (choices.Contains(text, StringComparer.Ordinal))
        {
            return text;
        }

        // A unique match that differs only in case is accepted and mapped to the declared spelling
        var loose = choices.Where(c => string.Equals(c, text, StringComparison.OrdinalIgnoreCase)).ToList();
        if (loose.Count == 1)
        {
            return loose[0];
        }

        error = $"'{text}' is not one of the allowed choices ({string.Join(", ", choices)}).";
        return null;
    }
}
