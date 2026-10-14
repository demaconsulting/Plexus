using System.ComponentModel;

namespace DemaConsulting.Plexus.Avalonia.PropertyGrid;

/// <summary>
///     Shows the properties of any .NET object, honoring the <c>System.ComponentModel</c> annotations the way the
///     WinForms property grid does. Properties are discovered with <see cref="TypeDescriptor.GetProperties(object)"/>,
///     so <see cref="ICustomTypeDescriptor"/> (dynamic properties) and <see cref="TypeDescriptionProvider"/> work.
/// </summary>
/// <remarks>
///     <para>
///         Supported annotations: <c>[Category]</c> (default "Misc"), <c>[Description]</c>, <c>[DisplayName]</c>,
///         <c>[ReadOnly]</c>, <c>[Browsable(false)]</c> (hidden), <c>[DefaultValue]</c> and the
///         <c>ShouldSerialize&lt;Name&gt;</c> / <c>Reset&lt;Name&gt;</c> methods (<see cref="PropertyItem.IsChanged"/>,
///         <see cref="PropertyItem.Reset"/>), <c>[TypeConverter]</c> (including
///         <see cref="ExpandableObjectConverter"/>), <c>[RefreshProperties]</c>, <c>[ParenthesizePropertyName]</c>,
///         <c>[PasswordPropertyText]</c>, <c>[DefaultProperty]</c>, and <see cref="PropertyEditorAttribute"/> (or,
///         when the property has none, <c>[UIHint("key")]</c> from <c>System.ComponentModel.DataAnnotations</c>,
///         which names the editor key without a reference to this assembly; <see cref="PropertyEditorAttribute"/> wins
///         when both are present). As
///         conveniences for code that is not WinForms-derived, <c>[Display(Name, Description, GroupName)]</c>,
///         <c>[Editable(false)]</c> and the validation attributes (<c>[Range]</c>, <c>[Required]</c>,
///         <c>[StringLength]</c>, ...) are honored too.
///     </para>
///     <para>
///         If the target implements <see cref="INotifyPropertyChanged"/> the source raises <see cref="Changed"/> for
///         each change notification; a change of a property annotated <c>[RefreshProperties(RefreshProperties.All)]</c>
///         also rebuilds the item list (see <see cref="Refresh"/>). Dispose the source to detach from the target.
///     </para>
/// </remarks>
/// <example>
///     <code><![CDATA[
///     public class Person
///     {
///         [Category("Identity"), Description("The full name.")]
///         public string Name { get; set; } = "";
///
///         [Category("Identity"), DefaultValue(0)]
///         public int Age { get; set; }
///
///         [Browsable(false)]
///         public string InternalId { get; set; } = "";
///     }
///
///     using var source = new ReflectionPropertySource(new Person { Name = "Ada", Age = 36 });
///     foreach (PropertyGroup group in PropertyOrdering.Group(source.GetItems(), PropertySortMode.Categorized))
///     {
///         Console.WriteLine(group.Category);
///         foreach (PropertyItem item in group.Items)
///         {
///             Console.WriteLine($"  {item.Label} = {item.GetText()}");
///         }
///     }
///     ]]></code>
/// </example>
public sealed class ReflectionPropertySource : IPropertySource, IDisposable
{
    /// <summary>
    ///     The object whose properties are shown.
    /// </summary>
    private readonly object _target;

    /// <summary>
    ///     Whether non-browsable properties are included.
    /// </summary>
    private readonly bool _includeNonBrowsable;

    /// <summary>
    ///     The current items.
    /// </summary>
    private IReadOnlyList<PropertyItem> _items;

    /// <summary>
    ///     Initializes a new instance of the <see cref="ReflectionPropertySource"/> class.
    /// </summary>
    /// <param name="target">The object whose properties are shown.</param>
    /// <param name="includeNonBrowsable">
    ///     <see langword="true"/> to include properties annotated <c>[Browsable(false)]</c> (they are marked with
    ///     <see cref="PropertyItem.IsBrowsable"/> = <see langword="false"/>); the default omits them.
    /// </param>
    /// <exception cref="ArgumentNullException"><paramref name="target"/> is <see langword="null"/>.</exception>
    public ReflectionPropertySource(object target, bool includeNonBrowsable = false)
    {
        ArgumentNullException.ThrowIfNull(target);
        _target = target;
        _includeNonBrowsable = includeNonBrowsable;
        _items = Build();
        if (target is INotifyPropertyChanged notifier)
        {
            notifier.PropertyChanged += OnTargetPropertyChanged;
        }
    }

    /// <inheritdoc />
    public event EventHandler<PropertySourceChangedEventArgs>? Changed;

    /// <summary>
    ///     Gets the object whose properties are shown.
    /// </summary>
    public object Target => _target;

    /// <summary>
    ///     Gets the name of the type's default property (the <c>[DefaultProperty]</c> annotation), which a grid selects
    ///     initially; <see langword="null"/> when there is none.
    /// </summary>
    public string? DefaultPropertyName => TypeDescriptor.GetDefaultProperty(_target)?.Name;

    /// <inheritdoc />
    public IReadOnlyList<PropertyItem> GetItems() => _items;

    /// <summary>
    ///     Discovers the properties again (a custom type descriptor may offer a different set now) and raises
    ///     <see cref="Changed"/> for everything. Previously returned items stay usable but are replaced in
    ///     <see cref="GetItems"/>.
    /// </summary>
    public void Refresh()
    {
        _items = Build();
        Changed?.Invoke(this, new PropertySourceChangedEventArgs());
    }

    /// <summary>
    ///     Detaches from the target's change notifications.
    /// </summary>
    public void Dispose()
    {
        if (_target is INotifyPropertyChanged notifier)
        {
            notifier.PropertyChanged -= OnTargetPropertyChanged;
        }
    }

    /// <summary>
    ///     Builds the item list from the target's property descriptors.
    /// </summary>
    /// <returns>The items.</returns>
    private List<PropertyItem> Build()
    {
        var items = new List<PropertyItem>();
        foreach (PropertyDescriptor descriptor in TypeDescriptor.GetProperties(_target))
        {
            if (_includeNonBrowsable || descriptor.IsBrowsable)
            {
                items.Add(new ReflectionPropertyItem(descriptor, () => _target, null));
            }
        }

        return items;
    }

    /// <summary>
    ///     Forwards a change notification of the target.
    /// </summary>
    /// <param name="sender">The target.</param>
    /// <param name="e">The notification.</param>
    private void OnTargetPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        var item = string.IsNullOrEmpty(e.PropertyName) ? null : _items.FirstOrDefault(i => i.Name == e.PropertyName);
        if (string.IsNullOrEmpty(e.PropertyName) || item is { RefreshMode: RefreshProperties.All })
        {
            Refresh();
            return;
        }

        Changed?.Invoke(this, new PropertySourceChangedEventArgs(e.PropertyName));
    }
}
