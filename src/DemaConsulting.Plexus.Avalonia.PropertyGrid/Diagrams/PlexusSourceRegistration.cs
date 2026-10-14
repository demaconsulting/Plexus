using System.Runtime.CompilerServices;

namespace DemaConsulting.Plexus.Avalonia.PropertyGrid.Diagrams;

/// <summary>
///     Registers the Plexus sources with <see cref="PropertySourceFactory"/> when the package loads, so a grid can be given
///     a <see cref="Node"/>, an <see cref="Annotation"/> or a <see cref="Group"/> directly.
/// </summary>
internal static class PlexusSourceRegistration
{
    /// <summary>
    ///     Registers the factory; runs once, when the assembly is loaded.
    /// </summary>
#pragma warning disable CA2255 // The package registers its Plexus adapters itself, so that `grid.SelectedObject = node` works without any setup call
    [ModuleInitializer]
#pragma warning restore CA2255
    internal static void Register() => PropertySourceFactory.Register(Create);

    /// <summary>
    ///     Creates the source of a Plexus element.
    /// </summary>
    /// <param name="target">The object a grid was given.</param>
    /// <returns>The source, or <see langword="null"/> when the object is not a Plexus element.</returns>
    internal static IPropertySource? Create(object target) => target switch
    {
        Node node => new NodePropertySource(node),
        Annotation annotation => new AnnotationPropertySource(annotation),
        Group group => new GroupPropertySource(group),
        _ => null,
    };
}
