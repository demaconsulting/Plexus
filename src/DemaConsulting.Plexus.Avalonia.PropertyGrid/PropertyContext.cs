using System.ComponentModel;

namespace DemaConsulting.Plexus.Avalonia.PropertyGrid;

/// <summary>
///     The <see cref="ITypeDescriptorContext"/> handed to type converters so that context-sensitive converters (for
///     example ones listing standard values that depend on the edited instance) work as they do in the WinForms grid.
/// </summary>
internal sealed class PropertyContext : ITypeDescriptorContext
{
    /// <summary>
    ///     The item being converted.
    /// </summary>
    private readonly PropertyItem _item;

    /// <summary>
    ///     Initializes a new instance of the <see cref="PropertyContext"/> class.
    /// </summary>
    /// <param name="item">The item being converted.</param>
    internal PropertyContext(PropertyItem item) => _item = item;

    /// <inheritdoc />
    public IContainer? Container => null;

    /// <inheritdoc />
    public object? Instance => _item.ContextInstance;

    /// <inheritdoc />
    public PropertyDescriptor? PropertyDescriptor => _item.Descriptor;

    /// <inheritdoc />
    public object? GetService(Type serviceType) => null;

    /// <inheritdoc />
    public bool OnComponentChanging() => true;

    /// <inheritdoc />
    public void OnComponentChanged()
    {
        // The grid does not track component change notifications; sessions raise their own events
    }
}
