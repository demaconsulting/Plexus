namespace DemaConsulting.Plexus.Avalonia.PropertyGrid;

/// <summary>
///     Chooses the <see cref="IPropertySource"/> for an object: a registered adapter, the object itself when it is a
///     source or a <see cref="IPropertySourceProvider"/>, otherwise reflection. Adapter layers (for example for Plexus
///     nodes) register themselves here so a grid can be given any object.
/// </summary>
/// <remarks>
///     Resolution order: registered factories, newest registration first (the first that returns a source wins); then
///     the object itself if it implements <see cref="IPropertySource"/>; then <see cref="IPropertySourceProvider"/>;
///     then a new <see cref="ReflectionPropertySource"/>. Registration is thread-safe. The caller owns the returned
///     source and should dispose it when it is <see cref="IDisposable"/>.
/// </remarks>
/// <example>
///     <code><![CDATA[
///     using IDisposable registration = PropertySourceFactory.Register(target =>
///         target is Gauge gauge ? new CustomPropertySource([new CustomPropertyItem("Value", typeof(double), () => gauge.Value)]) : null);
///
///     IPropertySource source = PropertySourceFactory.Create(new Gauge());
///     ]]></code>
/// </example>
public static class PropertySourceFactory
{
    /// <summary>
    ///     Guards the registrations.
    /// </summary>
    private static readonly Lock Gate = new();

    /// <summary>
    ///     The registered factories, oldest first.
    /// </summary>
    private static Func<object, IPropertySource?>[] _factories = [];

    /// <summary>
    ///     Registers an adapter factory.
    /// </summary>
    /// <param name="factory">Returns a source for the objects it understands and <see langword="null"/> for others.</param>
    /// <returns>A token that removes the registration when disposed.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="factory"/> is <see langword="null"/>.</exception>
    public static IDisposable Register(Func<object, IPropertySource?> factory)
    {
        ArgumentNullException.ThrowIfNull(factory);
        lock (Gate)
        {
            _factories = [.. _factories, factory];
        }

        return new Registration(factory);
    }

    /// <summary>
    ///     Creates the property source for an object.
    /// </summary>
    /// <param name="target">The object to show.</param>
    /// <returns>The source.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="target"/> is <see langword="null"/>.</exception>
    public static IPropertySource Create(object target)
    {
        ArgumentNullException.ThrowIfNull(target);
        var factories = Volatile.Read(ref _factories);
        for (var i = factories.Length - 1; i >= 0; i--)
        {
            var source = factories[i](target);
            if (source is not null)
            {
                return source;
            }
        }

        return target switch
        {
            IPropertySource self => self,
            IPropertySourceProvider provider => provider.CreatePropertySource(),
            _ => new ReflectionPropertySource(target),
        };
    }

    /// <summary>
    ///     The registration token.
    /// </summary>
    /// <param name="factory">The registered factory.</param>
    private sealed class Registration(Func<object, IPropertySource?> factory) : IDisposable
    {
        /// <summary>
        ///     Removes the registration.
        /// </summary>
        public void Dispose()
        {
            lock (Gate)
            {
                var index = Array.IndexOf(_factories, factory);
                if (index >= 0)
                {
#pragma warning disable S2696 // The registration token owns removing its factory from the shared list
                    _factories = [.. _factories.Where((_, i) => i != index)];
#pragma warning restore S2696
                }
            }
        }
    }
}
