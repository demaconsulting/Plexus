namespace DemaConsulting.Plexus.Avalonia.PropertyGrid;

/// <summary>
///     Implemented by an object that supplies its own <see cref="IPropertySource"/>, so a grid can show it without
///     reflection (see <see cref="PropertySourceFactory"/>).
/// </summary>
/// <example>
///     <code><![CDATA[
///     using DemaConsulting.Plexus.Avalonia.PropertyGrid;
///
///     public sealed class Gauge : IPropertySourceProvider
///     {
///         public double Value { get; set; }
///
///         public IPropertySource CreatePropertySource() => new CustomPropertySource(
///         [
///             new CustomPropertyItem("Value", typeof(double), () => Value, v => Value = (double)v!),
///         ]);
///     }
///     ]]></code>
/// </example>
public interface IPropertySourceProvider
{
    /// <summary>
    ///     Creates the property source for this object.
    /// </summary>
    /// <returns>The source.</returns>
    IPropertySource CreatePropertySource();
}
