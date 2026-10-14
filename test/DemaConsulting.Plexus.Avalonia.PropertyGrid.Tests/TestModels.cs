using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Globalization;

namespace DemaConsulting.Plexus.Avalonia.PropertyGrid.Tests;

/// <summary>
///     A length unit enumeration used as an enum-typed property.
/// </summary>
public enum Unit
{
    /// <summary>Meters.</summary>
    Meters,

    /// <summary>Feet.</summary>
    Feet,

    /// <summary>Miles.</summary>
    Miles,
}

/// <summary>
///     A flags enumeration (its converter is not exclusive).
/// </summary>
[Flags]
public enum Options
{
    /// <summary>No option.</summary>
    None = 0,

    /// <summary>Fast.</summary>
    Fast = 1,

    /// <summary>Quiet.</summary>
    Quiet = 2,
}

/// <summary>
///     A reference-type expandable object.
/// </summary>
[TypeConverter(typeof(ExpandableObjectConverter))]
public sealed class Address
{
    /// <summary>Gets or sets the street.</summary>
    public string Street { get; set; } = string.Empty;

    /// <summary>Gets or sets the city.</summary>
    [DefaultValue("Paris")]
    public string City { get; set; } = "Paris";

    /// <inheritdoc />
    public override string ToString() => $"{Street}, {City}";
}

/// <summary>
///     A value-type expandable object.
/// </summary>
[TypeConverter(typeof(ExpandableObjectConverter))]
public struct Dimensions
{
    /// <summary>Gets or sets the width.</summary>
    public double Width { get; set; }

    /// <summary>Gets or sets the height.</summary>
    public double Height { get; set; }
}

/// <summary>
///     A collection element with a public default constructor.
/// </summary>
[TypeConverter(typeof(ExpandableObjectConverter))]
public sealed class Tag
{
    /// <summary>Gets or sets the label.</summary>
    public string Label { get; set; } = string.Empty;

    /// <inheritdoc />
    public override string ToString() => Label;
}

/// <summary>
///     A collection element without a default constructor.
/// </summary>
public sealed class Badge
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="Badge"/> class.
    /// </summary>
    /// <param name="level">The level.</param>
    public Badge(int level) => Level = level;

    /// <summary>Gets the level.</summary>
    public int Level { get; }
}

/// <summary>
///     A value converted from and to text by a custom converter that honors the culture.
/// </summary>
/// <param name="Degrees">The degrees.</param>
[TypeConverter(typeof(TemperatureConverter))]
public readonly record struct Temperature(double Degrees);

/// <summary>
///     Converts <see cref="Temperature"/> to and from text such as <c>12.5°</c>.
/// </summary>
public sealed class TemperatureConverter : TypeConverter
{
    /// <inheritdoc />
    public override bool CanConvertFrom(ITypeDescriptorContext? context, Type sourceType) =>
        sourceType == typeof(string) || base.CanConvertFrom(context, sourceType);

    /// <inheritdoc />
    public override object? ConvertFrom(ITypeDescriptorContext? context, CultureInfo? culture, object value) =>
        value is string text
            ? new Temperature(double.Parse(text.Trim().TrimEnd('°'), culture ?? CultureInfo.InvariantCulture))
            : base.ConvertFrom(context, culture, value);

    /// <inheritdoc />
    public override object? ConvertTo(ITypeDescriptorContext? context, CultureInfo? culture, object? value, Type destinationType) =>
        destinationType == typeof(string) && value is Temperature temperature
            ? temperature.Degrees.ToString("0.0", culture ?? CultureInfo.InvariantCulture) + "°"
            : base.ConvertTo(context, culture, value, destinationType);
}

/// <summary>
///     A converter whose standard values depend on the edited instance (the context).
/// </summary>
public sealed class ChoiceConverter : StringConverter
{
    /// <inheritdoc />
    public override bool GetStandardValuesSupported(ITypeDescriptorContext? context) => true;

    /// <inheritdoc />
    public override bool GetStandardValuesExclusive(ITypeDescriptorContext? context) => false;

    /// <inheritdoc />
    public override StandardValuesCollection? GetStandardValues(ITypeDescriptorContext? context) =>
        new(context?.Instance is Person person ? new[] { "first-" + person.Name, "second" } : new[] { "none" });
}

/// <summary>
///     A general-purpose annotated test object that exercises every supported annotation.
/// </summary>
public class Person
{
    /// <summary>Gets or sets the name.</summary>
    [Category("Identity")]
    [Description("The full name.")]
    [DisplayName("Full Name")]
    public string Name { get; set; } = string.Empty;

    /// <summary>Gets or sets the age.</summary>
    [Category("Identity")]
    [DefaultValue(0)]
    public int Age { get; set; }

    /// <summary>Gets or sets a property without any annotation.</summary>
    public string Nickname { get; set; } = string.Empty;

    /// <summary>Gets or sets a read-only (annotated) identifier.</summary>
    [ReadOnly(true)]
    public string Id { get; set; } = "P-1";

    /// <summary>Gets a getter-only (implicitly read-only) value.</summary>
    public string Summary => $"{Name} ({Age})";

    /// <summary>Gets or sets a hidden value.</summary>
    [Browsable(false)]
    public string Secret { get; set; } = "hidden";

    /// <summary>Gets or sets a masked value.</summary>
    [PasswordPropertyText(true)]
    public string Password { get; set; } = "pw";

    /// <summary>Gets or sets a parenthesized value.</summary>
    [ParenthesizePropertyName(true)]
    public string Handle { get; set; } = "h";

    /// <summary>Gets or sets a value that requires every property to be re-read when it changes.</summary>
    [RefreshProperties(RefreshProperties.All)]
    public Unit Units { get; set; }

    /// <summary>Gets or sets a flags value.</summary>
    public Options Options { get; set; }

    /// <summary>Gets or sets a boolean.</summary>
    [DefaultValue(false)]
    public bool Active { get; set; }

    /// <summary>Gets or sets a value with a default rule instead of a default value.</summary>
    public int Level { get; set; } = 5;

    /// <summary>Gets or sets a double (culture-sensitive text).</summary>
    public double Ratio { get; set; }

    /// <summary>Gets or sets a temperature (custom converter).</summary>
    public Temperature Temperature { get; set; }

    /// <summary>Gets or sets a nullable integer.</summary>
    public int? Limit { get; set; }

    /// <summary>Gets or sets a validated percentage.</summary>
    [Range(0, 100, ErrorMessage = "Percent must be between 0 and 100.")]
    public int Percent { get; set; }

    /// <summary>Gets or sets a value described with data annotations instead of component model attributes.</summary>
    [Display(Name = "Postal Code", Description = "Code used for delivery.", GroupName = "Postal")]
    public string PostalCode { get; set; } = string.Empty;

    /// <summary>Gets or sets a value that only data annotations mark as non-editable.</summary>
    [Editable(false)]
    public string Locked { get; set; } = "L";

    /// <summary>Gets or sets a string with instance-dependent standard values.</summary>
    [TypeConverter(typeof(ChoiceConverter))]
    public string Choice { get; set; } = string.Empty;

    /// <summary>Gets or sets a value with a custom editor.</summary>
    [PropertyEditor("color")]
    public string Tint { get; set; } = "#FF8800";

    /// <summary>Gets or sets an expandable reference object.</summary>
    public Address Address { get; set; } = new();

    /// <summary>Gets or sets an expandable value type.</summary>
    public Dimensions Size { get; set; }

    /// <summary>Gets a get-only list (the instance is owned by the object).</summary>
    public List<string> Notes { get; } = [];

    /// <summary>Gets or sets a list of expandable elements.</summary>
    public List<Tag> Tags { get; set; } = [];

    /// <summary>Gets or sets an array (fixed length).</summary>
    public int[] Scores { get; set; } = [];

    /// <summary>Gets a get-only array.</summary>
    public string[] Codes { get; } = ["a", "b"];

    /// <summary>Gets a read-only list.</summary>
    public IReadOnlyList<string> History { get; } = new List<string> { "x", "y" }.AsReadOnly();

    /// <summary>Gets or sets a set (<see cref="ICollection{T}"/> only).</summary>
    public HashSet<string> Labels { get; set; } = [];

    /// <summary>Gets or sets a list whose elements cannot be created by default.</summary>
    public List<Badge> Badges { get; set; } = [];

    /// <summary>Gets or sets a dictionary (not treated as a collection).</summary>
    public Dictionary<string, int> Map { get; set; } = [];

    /// <summary>Gets or sets a throwing property.</summary>
    [DefaultValue(0)]
    public int Fragile
    {
        get => 0;
        set => throw new InvalidOperationException("Fragile does not accept values.");
    }

    /// <summary>Gets or sets an object-typed property.</summary>
    public object? Anything { get; set; }

#pragma warning disable S1144 // Found by the component model through reflection (the WinForms default-rule convention)
    /// <summary>
    ///     Reports whether <see cref="Level"/> must be saved (the WinForms default-rule convention).
    /// </summary>
    /// <returns><see langword="true"/> when the level differs from 5.</returns>
    private bool ShouldSerializeLevel() => Level != 5;

    /// <summary>
    ///     Restores the default <see cref="Level"/>.
    /// </summary>
    private void ResetLevel() => Level = 5;
#pragma warning restore S1144
}

/// <summary>
///     A change-notifying object.
/// </summary>
public sealed class Observable : INotifyPropertyChanged
{
    /// <summary>
    ///     The title.
    /// </summary>
    private string _title = string.Empty;

    /// <inheritdoc />
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>Gets or sets the title.</summary>
    public string Title
    {
        get => _title;
        set
        {
            _title = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Title)));
        }
    }

    /// <summary>
    ///     Raises a change notification for all properties.
    /// </summary>
    public void NotifyAll() => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(null));
}

/// <summary>
///     An object with no public properties.
/// </summary>
#pragma warning disable S2094 // A type without properties is the point of this test object
public sealed class Empty;
#pragma warning restore S2094

/// <summary>
///     A small settings object used by the system-level scenarios.
/// </summary>
public sealed class Mixer
{
    /// <summary>Gets or sets the gain.</summary>
    [Category("Levels")]
    [Description("Output level in dB.")]
    [DefaultValue(0.0)]
    public double Gain { get; set; }

    /// <summary>Gets or sets a value indicating whether the output is muted.</summary>
    [Category("Levels")]
    public bool Muted { get; set; }

    /// <summary>Gets or sets the channel names.</summary>
    [Category("Channels")]
    public List<string> Channels { get; set; } = ["left", "right"];

    /// <summary>Gets or sets the identifier (hidden).</summary>
    [Browsable(false)]
    public string Id { get; set; } = "m1";
}

/// <summary>
///     A change-notifying object with a property that requires everything to be re-read.
/// </summary>
public sealed class Refreshing : INotifyPropertyChanged
{
    /// <summary>
    ///     The mode.
    /// </summary>
    private int _mode;

    /// <summary>
    ///     The other value.
    /// </summary>
    private int _other;

    /// <inheritdoc />
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>Gets or sets the mode.</summary>
    [RefreshProperties(RefreshProperties.All)]
    public int Mode
    {
        get => _mode;
        set
        {
            _mode = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Mode)));
        }
    }

    /// <summary>Gets or sets the other value.</summary>
    public int Other
    {
        get => _other;
        set
        {
            _other = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Other)));
        }
    }
}
