using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace DemaConsulting.Plexus.Avalonia.PropertyGrid;

/// <summary>
///     One editable property of an object (or of a nested object or collection element): its metadata, its current
///     value, and the text, standard-value, expandable-object and collection behavior derived from its type converter.
/// </summary>
/// <remarks>
///     <para>
///         A <see cref="PropertyItem"/> is the grid's view-model for a single row. It is independent of any UI framework
///         and of Plexus types. Items are produced by an <see cref="IPropertySource"/>
///         (<see cref="ReflectionPropertySource"/> for annotated objects, <see cref="CustomPropertyItem"/> for hosts
///         that supply values themselves); nested items come from <see cref="Children"/> and
///         <see cref="GetCollectionItems"/>.
///     </para>
///     <para>
///         Reading and writing: <see cref="GetValue"/> and <see cref="SetValue"/> may throw whatever the underlying
///         accessor throws. The <c>Try...</c> members (<see cref="TryGetValue"/>, <see cref="TrySetValue"/>,
///         <see cref="TrySetText"/>) never throw for user-code failures and report a <see cref="PropertyResult"/>
///         instead, which is what a user interface should call.
///     </para>
///     <para>
///         Culture: text conversion (<see cref="ConvertToString"/>, <see cref="ConvertFromString"/>,
///         <see cref="TryParse"/>, <see cref="TrySetText"/>) uses <see cref="System.Globalization.CultureInfo.CurrentCulture"/>
///         by default, matching what a user typing a number or date expects to see and enter (the WinForms property
///         grid behaves the same). Pass <see cref="System.Globalization.CultureInfo.InvariantCulture"/> explicitly for
///         a culture-independent representation, for example for persisted text.
///     </para>
///     <para>
///         Deriving: most hosts use <see cref="CustomPropertyItem"/> instead of deriving. A derived class must implement the four abstract members
///         <see cref="Name"/>, <see cref="PropertyType"/>, <see cref="GetValue"/> and <see cref="SetValue"/> (the constructor is protected and takes the parent item for nested
///         items). Every other metadata member is virtual with a neutral default and can be overridden: <see cref="DisplayName"/>, <see cref="Category"/>,
///         <see cref="CategoryOrder"/>, <see cref="Order"/>, <see cref="Description"/>, <see cref="IsReadOnly"/>, <see cref="IsBrowsable"/>, <see cref="IsPasswordText"/>,
///         <see cref="IsParenthesized"/>, <see cref="RefreshMode"/>, <see cref="EditorKey"/>, <see cref="EditorType"/>, <see cref="HasDefault"/>,
///         <see cref="IsDefaultValueKnown"/>, <see cref="DefaultValue"/>, <see cref="IsChanged"/>, <see cref="CanReset"/>, <see cref="Converter"/> and <see cref="Reset"/>.
///         <c>GetValidationAttributes</c> is the one protected virtual member: return the validation attributes that apply to the item.
///     </para>
/// </remarks>
/// <example>
///     <code><![CDATA[
///     using DemaConsulting.Plexus.Avalonia.PropertyGrid;
///
///     using var source = new ReflectionPropertySource(new Person { Name = "Ada", Age = 36 });
///     foreach (PropertyItem item in source.GetItems())
///     {
///         string text = item.ConvertToString(item.GetValue());
///         Console.WriteLine($"{item.Category}/{item.Label}: {text}{(item.IsChanged ? " (changed)" : "")}");
///     }
///
///     PropertyItem age = source.GetItems().First(i => i.Name == "Age");
///     PropertyResult result = age.TrySetText("abc");   // fails: "abc" is not an Int32
///
///     public class Person
///     {
///         public string Name { get; set; } = "";
///         public int Age { get; set; }
///     }
///     ]]></code>
/// </example>
public abstract partial class PropertyItem
{
    /// <summary>
    ///     The category used when a property has no category annotation (the same default as WinForms).
    /// </summary>
    public const string DefaultCategory = "Misc";

    /// <summary>
    ///     Initializes a new instance of the <see cref="PropertyItem"/> class.
    /// </summary>
    /// <param name="parent">The item this item is nested under, or <see langword="null"/> for a top-level item.</param>
    protected PropertyItem(PropertyItem? parent = null) => Parent = parent;

    /// <summary>
    ///     Gets the item this item is nested under, or <see langword="null"/> for a top-level item.
    /// </summary>
    public PropertyItem? Parent { get; }

    /// <summary>
    ///     Gets the property name (stable identifier, not shown to the user).
    /// </summary>
    public abstract string Name { get; }

    /// <summary>
    ///     Gets the display name (the <c>DisplayName</c> annotation, else the name).
    /// </summary>
    public virtual string DisplayName => Name;

    /// <summary>
    ///     Gets the text to show as the row label: the display name, wrapped in parentheses when
    ///     <see cref="IsParenthesized"/> is set.
    /// </summary>
    public string Label => IsParenthesized ? $"({DisplayName})" : DisplayName;

    /// <summary>
    ///     Gets the category (the <c>Category</c> annotation, else <see cref="DefaultCategory"/>).
    /// </summary>
    public virtual string Category => DefaultCategory;

    /// <summary>
    ///     Gets the rank of the item's category when a grid sorts by category: categories are shown by ascending rank and,
    ///     within the same rank, alphabetically. The rank of a category is the lowest rank of its items, so a source can
    ///     put a category such as "General" first. The default is 0 (alphabetical order only).
    /// </summary>
    public virtual int CategoryOrder => 0;

    /// <summary>
    ///     Gets the rank of the item within its category when a grid sorts by category: items are shown by ascending rank
    ///     (after parenthesized names) and, within the same rank, alphabetically by <see cref="DisplayName"/>. The default
    ///     is 0 (alphabetical order only).
    /// </summary>
    /// <remarks>
    ///     Lets a source present related properties in a meaningful sequence rather than alphabetically. The alphabetical
    ///     view (<see cref="PropertySortMode.Alphabetical"/>) ignores it.
    /// </remarks>
    public virtual int Order => 0;

    /// <summary>
    ///     Gets the description shown for the property (empty when there is none).
    /// </summary>
    public virtual string Description => string.Empty;

    /// <summary>
    ///     Gets the declared type of the property.
    /// </summary>
    public abstract Type PropertyType { get; }

    /// <summary>
    ///     Gets a value indicating whether the value cannot be edited.
    /// </summary>
    public virtual bool IsReadOnly => false;

    /// <summary>
    ///     Gets a value indicating whether the property is shown in a grid. Sources omit non-browsable items unless
    ///     asked to include them.
    /// </summary>
    public virtual bool IsBrowsable => true;

    /// <summary>
    ///     Gets a value indicating whether the value is a secret that a user interface should mask.
    /// </summary>
    public virtual bool IsPasswordText => false;

    /// <summary>
    ///     Gets a value indicating whether the name is shown in parentheses and sorted before the other names.
    /// </summary>
    public virtual bool IsParenthesized => false;

    /// <summary>
    ///     Gets what a user interface should refresh after the value of this property changes:
    ///     <see cref="RefreshProperties.All"/> means every property must be re-read.
    /// </summary>
    public virtual RefreshProperties RefreshMode => RefreshProperties.None;

    /// <summary>
    ///     Gets the key of the custom editor requested through <see cref="PropertyEditorAttribute"/> or, when the
    ///     property has none, through a <c>[UIHint]</c> annotation, or <see langword="null"/> to use the registry's
    ///     choice for the property type.
    /// </summary>
    public virtual string? EditorKey => null;

    /// <summary>
    ///     Gets the editor type named by <see cref="PropertyEditorAttribute"/>, when the annotation used a type.
    /// </summary>
    public virtual Type? EditorType => null;

    /// <summary>
    ///     Gets a value indicating whether the property has default semantics, either a default value or a rule that
    ///     tells whether the current value differs from the default. Only such properties can be
    ///     <see cref="IsChanged"/>.
    /// </summary>
    public virtual bool HasDefault => false;

    /// <summary>
    ///     Gets a value indicating whether <see cref="DefaultValue"/> holds the default value (a
    ///     <see cref="HasDefault"/> property can have a default rule but no known default value).
    /// </summary>
    public virtual bool IsDefaultValueKnown => HasDefault;

    /// <summary>
    ///     Gets the default value; meaningful only when <see cref="IsDefaultValueKnown"/> is <see langword="true"/>.
    /// </summary>
    public virtual object? DefaultValue => null;

    /// <summary>
    ///     Gets a value indicating whether the current value differs from the default, which a user interface shows in
    ///     bold. Always <see langword="false"/> when <see cref="HasDefault"/> is <see langword="false"/>.
    /// </summary>
    public virtual bool IsChanged
    {
        get
        {
            if (!HasDefault || !IsDefaultValueKnown)
            {
                return false;
            }

            return TryGetValue(out var value).Succeeded && !Equals(value, DefaultValue);
        }
    }

    /// <summary>
    ///     Gets a value indicating whether <see cref="Reset"/> can restore the default value.
    /// </summary>
    public virtual bool CanReset => HasDefault && IsDefaultValueKnown && !IsReadOnly;

    /// <summary>
    ///     Gets the unique path of the item within its source: the item's root path for a top-level item (its name, unless the
    ///     source prefixes it, as the node source does with <c>Node.</c>, <c>Property.</c> and <c>Data.</c>), with
    ///     <c>.Name</c> appended for each nested property and <c>[index]</c> for each collection element
    ///     (for example <c>Address.City</c>, <c>Tags[2]</c> or, in a node source, <c>Data.Address.City</c>).
    /// </summary>
    /// <remarks>
    ///     The path is what rows, pending edits, errors and automation ids are keyed by: grid rows expose
    ///     <c>PropertyGrid_Row_{path}</c>. Find an item from its path with <see cref="PropertyEditSession.FindItem"/>.
    /// </remarks>
    public string Path
    {
        get
        {
            if (Parent is null)
            {
                return RootPath;
            }

            return Parent.Path + (IsIndexer ? string.Empty : ".") + Name;
        }
    }

    /// <summary>
    ///     Gets the path of the item when it is top-level; the name unless a source prefixes it to keep the paths of its groups of rows apart.
    /// </summary>
    internal virtual string RootPath => Name;

    /// <summary>
    ///     Gets a value indicating whether the item's name is an index suffix such as <c>[2]</c>.
    /// </summary>
    internal virtual bool IsIndexer => false;

    /// <summary>
    ///     Gets the object whose property this item is, passed to type converters as the context instance.
    /// </summary>
    internal virtual object? ContextInstance
    {
        get
        {
            object? parentValue = null;
            return Parent?.TryGetValue(out parentValue).Succeeded == true ? parentValue : null;
        }
    }

    /// <summary>
    ///     Gets the property descriptor backing this item, or <see langword="null"/> when the item is not
    ///     descriptor-based.
    /// </summary>
    internal virtual PropertyDescriptor? Descriptor => null;

    /// <summary>
    ///     Gets the validation annotations (<see cref="ValidationAttribute"/>) that apply to values of this item.
    /// </summary>
    /// <returns>The attributes; empty by default.</returns>
    protected virtual IEnumerable<ValidationAttribute> GetValidationAttributes() => [];

    /// <summary>
    ///     Reads the current value; may throw whatever the underlying accessor throws.
    /// </summary>
    /// <returns>The current value.</returns>
    public abstract object? GetValue();

    /// <summary>
    ///     Writes a value without validation or read-only checks; may throw whatever the underlying accessor throws.
    ///     User interfaces should call <see cref="TrySetValue"/> instead.
    /// </summary>
    /// <param name="value">The new value.</param>
    public abstract void SetValue(object? value);

    /// <summary>
    ///     Restores the default value (see <see cref="CanReset"/>).
    /// </summary>
    public virtual void Reset() => SetValue(DefaultValue);

    /// <summary>
    ///     Reads the current value, capturing exceptions as an error.
    /// </summary>
    /// <param name="value">The value, or <see langword="null"/> when reading failed.</param>
    /// <returns>The outcome of the read.</returns>
    public PropertyResult TryGetValue(out object? value)
    {
        try
        {
            value = GetValue();
            return PropertyResult.Success;
        }
        catch (Exception ex) when (!PropertyErrors.IsFatal(ex))
        {
            value = null;
            return PropertyResult.Failure(PropertyErrors.Message(ex));
        }
    }

    /// <summary>
    ///     Checks a candidate value without writing it: the value must be assignable to <see cref="PropertyType"/>
    ///     (<see langword="null"/> is only valid for reference and nullable types) and must satisfy the property's
    ///     validation annotations (<c>[Range]</c>, <c>[Required]</c>, ...).
    /// </summary>
    /// <param name="value">The candidate value.</param>
    /// <returns>The outcome of the check.</returns>
    public PropertyResult Validate(object? value)
    {
        if (value is null)
        {
            if (PropertyType.IsValueType && Nullable.GetUnderlyingType(PropertyType) is null)
            {
                return PropertyResult.Failure($"'{DisplayName}' cannot be empty.");
            }
        }
        else if (!PropertyType.IsInstanceOfType(value))
        {
            return PropertyResult.Failure($"A value of type {value.GetType().Name} is not valid for '{DisplayName}' ({PropertyType.Name}).");
        }

        return ValidateAnnotations(value);
    }

    /// <summary>
    ///     Evaluates the validation annotations against a value.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <returns>The outcome.</returns>
    private PropertyResult ValidateAnnotations(object? value)
    {
        try
        {
            var attributes = GetValidationAttributes().ToList();
            if (attributes.Count == 0)
            {
                return PropertyResult.Success;
            }

            var context = new ValidationContext(ContextInstance ?? this) { MemberName = Name, DisplayName = DisplayName };
            var errors = new List<ValidationResult>();
            return Validator.TryValidateValue(value!, context, errors, attributes)
                ? PropertyResult.Success
                : PropertyResult.Failure(errors[0].ErrorMessage ?? $"'{DisplayName}' is not valid.");
        }
        catch (Exception ex) when (!PropertyErrors.IsFatal(ex))
        {
            return PropertyResult.Failure(PropertyErrors.Message(ex));
        }
    }

    /// <summary>
    ///     Writes a value after checking that the item is editable and the value is valid, capturing exceptions
    ///     raised by the setter as an error.
    /// </summary>
    /// <param name="value">The new value.</param>
    /// <returns>The outcome of the write.</returns>
    public PropertyResult TrySetValue(object? value)
    {
        if (IsReadOnly)
        {
            return PropertyResult.Failure($"'{DisplayName}' is read-only.");
        }

        var valid = Validate(value);
        return valid.Succeeded ? PropertyErrors.Run(() => SetValue(value)) : valid;
    }

    /// <summary>
    ///     Restores the default value, capturing exceptions as an error.
    /// </summary>
    /// <returns>The outcome of the reset.</returns>
    public PropertyResult TryReset() =>
        CanReset ? PropertyErrors.Run(Reset) : PropertyResult.Failure($"'{DisplayName}' cannot be reset.");
}
