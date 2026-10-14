using Avalonia;

namespace DemaConsulting.Plexus.Avalonia.PropertyGrid.Diagrams;

/// <summary>
///     What a live preview of a node sees: the effective (staged) value of every row of the node's grid and a notification
///     whenever those values change. It reads only; it never writes to the node.
/// </summary>
/// <remarks>
///     <para>
///         <b>Effective values.</b> <see cref="GetValue"/> returns, for any row path (<c>Node.Name</c>,
///         <c>Property.&lt;name&gt;</c>, <c>Data.&lt;path&gt;</c>, see <see cref="NodePropertySource"/>), the pending edit when
///         there is one (the default value for a pending reset, the working list for a pending collection edit) and the
///         node's current value otherwise, through <see cref="PropertyEditSession.FindItem"/> and
///         <see cref="PropertyEditSession.GetValue"/>. Values keep their kinds: numbers, booleans and strings as declared by
///         the <see cref="PropertyDefinition"/>, choices as strings, colors as <c>Avalonia.Media.Color</c>.
///     </para>
///     <para>
///         <b>Notifications.</b> <see cref="Changed"/> is raised when a pending edit is made, replaced, reset or discarded,
///         when all pending edits are cancelled, after a commit or a live write, and when the grid replaces its session (its
///         content or edit mode changed, or it returned to the visual tree). It is not raised for a validation error alone,
///         because an error changes no value. Text editors stage their value on Enter or when they lose focus; drop-downs, check
///         boxes and sliders stage at once. The preview therefore redraws when a field is committed, not on every keystroke.
///     </para>
///     <para>
///         <b>Lifetime.</b> The context subscribes to the grid and its session; <see cref="Dispose"/> unsubscribes, after
///         which <see cref="Changed"/> is no longer raised and reads keep working on the last session.
///         <see cref="NodePropertiesDialog"/> disposes the context it creates when the dialog closes. A host that builds its
///         own window creates the context over its own staged <see cref="PropertyGrid"/> (or a
///         <see cref="PropertyEditSession"/>) and disposes it when the window closes.
///     </para>
///     <para>
///         <b>Threading.</b> Not thread-safe: create it, read it and dispose it on the UI thread; <see cref="Changed"/> is raised
///         on the UI thread.
///     </para>
/// </remarks>
/// <example>
///     A host-built inspector window: a staged grid next to a preview that redraws while the user edits.
///     <code><![CDATA[
///     using Avalonia.Controls;
///     using DemaConsulting.Plexus;
///     using DemaConsulting.Plexus.Avalonia.PropertyGrid;
///     using DemaConsulting.Plexus.Avalonia.PropertyGrid.Diagrams;
///
///     public sealed class InspectorWindow : Window
///     {
///         public InspectorWindow(Node node)
///         {
///             var grid = new PropertyGrid { EditMode = PropertyEditMode.Staged, SelectedObject = node };
///             var preview = new TextBlock { Width = 200 };
///             var context = new NodePreviewContext(grid);                     // follows grid.Session
///             void Redraw() => preview.Text = $"{context.GetValue(NodePropertySource.NamePath)}: {context.GetPropertyValue("Frequency")} Hz";
///             context.Changed += (_, _) => Redraw();
///             Redraw();
///             Closed += (_, _) => context.Dispose();
///
///             var panel = new DockPanel();
///             DockPanel.SetDock(preview, Dock.Right);
///             panel.Children.Add(preview);
///             panel.Children.Add(grid);
///             Content = panel;                                                // commit with grid.Session!.Commit(), discard with Cancel()
///         }
///     }
///     ]]></code>
/// </example>
public sealed class NodePreviewContext : IDisposable
{
    /// <summary>
    ///     The grid whose session is followed, or <see langword="null"/> for a context over a fixed session.
    /// </summary>
    private readonly PropertyGrid? _grid;

    /// <summary>
    ///     The node that was shown when the context was created (used when the current session shows no node).
    /// </summary>
    private readonly Node _node;

    /// <summary>
    ///     The session the values are read through, or <see langword="null"/> when the grid currently has none.
    /// </summary>
    private PropertyEditSession? _session;

    /// <summary>
    ///     Whether <see cref="Dispose"/> has run.
    /// </summary>
    private bool _disposed;

    /// <summary>
    ///     Initializes a new instance of the <see cref="NodePreviewContext"/> class over a grid that shows a node; the
    ///     context follows <see cref="PropertyGrid.Session"/> when the grid replaces it.
    /// </summary>
    /// <param name="grid">The grid; it must currently show a node (its session's source is a <see cref="NodePropertySource"/>).</param>
    /// <exception cref="ArgumentNullException"><paramref name="grid"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="grid"/> does not show a node.</exception>
    public NodePreviewContext(PropertyGrid grid)
    {
        ArgumentNullException.ThrowIfNull(grid);
        if (grid.Session?.Source is not NodePropertySource source)
        {
            throw new ArgumentException("The grid does not show a node.", nameof(grid));
        }

        _grid = grid;
        _node = source.Node;
        Attach(grid.Session);
        grid.PropertyChanged += OnGridPropertyChanged;
    }

    /// <summary>
    ///     Initializes a new instance of the <see cref="NodePreviewContext"/> class over a fixed session of a node, for hosts
    ///     that drive a session without a grid or keep the session themselves.
    /// </summary>
    /// <param name="session">The session; its source must be a <see cref="NodePropertySource"/>.</param>
    /// <exception cref="ArgumentNullException"><paramref name="session"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">The source of <paramref name="session"/> is not a <see cref="NodePropertySource"/>.</exception>
    public NodePreviewContext(PropertyEditSession session)
    {
        ArgumentNullException.ThrowIfNull(session);
        if (session.Source is not NodePropertySource source)
        {
            throw new ArgumentException("The session does not edit a node.", nameof(session));
        }

        _node = source.Node;
        Attach(session);
    }

    /// <summary>
    ///     Occurs on the UI thread, with <see cref="EventArgs.Empty"/>, whenever the effective values may have changed: a
    ///     pending edit was made, reset or discarded, the edits were cancelled or committed, a live write happened, or the
    ///     grid replaced its session. Read the values again with <see cref="GetValue"/> in the handler.
    /// </summary>
    public event EventHandler? Changed;

    /// <summary>
    ///     Gets the node that is shown. Read it (its type, ports or current values) but never modify it from a preview: the
    ///     dialog's OK and Cancel own every change.
    /// </summary>
    public Node Node => (_session?.Source as NodePropertySource)?.Node ?? _node;

    /// <summary>
    ///     Gets the effective value of a row: the pending edit when there is one, otherwise the node's current value.
    /// </summary>
    /// <param name="path">
    ///     The row path, for example <see cref="NodePropertySource.NamePath"/>, <c>NodePropertySource.PropertyPath("Gain")</c>
    ///     or <c>NodePropertySource.DataPath("Contact.Email")</c>.
    /// </param>
    /// <returns>
    ///     The value (for a pending reset the default value, for a pending collection edit the working list), or
    ///     <see langword="null"/> when the path names no row, the value cannot be read or the grid currently has no session.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="path"/> is <see langword="null"/>.</exception>
    public object? GetValue(string path)
    {
        ArgumentNullException.ThrowIfNull(path);
        return _session is { } session && session.FindItem(path) is { } item ? session.GetValue(item) : null;
    }

    /// <summary>
    ///     Gets the effective value of a property declared by the node type (the row <c>Property.&lt;name&gt;</c>).
    /// </summary>
    /// <param name="propertyName">The property name as declared by the node type, for example <c>"Frequency"</c>.</param>
    /// <returns>The value, see <see cref="GetValue"/>; <see langword="null"/> when the node type declares no such property.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="propertyName"/> is <see langword="null"/>.</exception>
    public object? GetPropertyValue(string propertyName)
    {
        ArgumentNullException.ThrowIfNull(propertyName);
        return GetValue(NodePropertySource.PropertyPath(propertyName));
    }

    /// <summary>
    ///     Stops listening to the grid and the session; <see cref="Changed"/> is no longer raised. Reads keep working on
    ///     the last session. Calling it again does nothing.
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        if (_grid is not null)
        {
            _grid.PropertyChanged -= OnGridPropertyChanged;
        }

        if (_session is not null)
        {
            _session.Changed -= OnSessionChanged;
        }

        Changed = null;
    }

    /// <summary>
    ///     Starts reading through a session and listening to it, after letting go of the previous one.
    /// </summary>
    /// <param name="session">The new session, or <see langword="null"/> when the grid has none.</param>
    private void Attach(PropertyEditSession? session)
    {
        if (_session is not null)
        {
            _session.Changed -= OnSessionChanged;
        }

        _session = session;
        if (_session is not null)
        {
            _session.Changed += OnSessionChanged;
        }
    }

    /// <summary>
    ///     Follows the grid's session: a new session means every value may have changed.
    /// </summary>
    /// <param name="sender">The grid.</param>
    /// <param name="e">The event data.</param>
    private void OnGridPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property != PropertyGrid.SessionProperty || _disposed)
        {
            return;
        }

        Attach(_grid!.Session);
        Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    ///     Forwards the session's value changes; an error alone changes no value and is not forwarded.
    /// </summary>
    /// <param name="sender">The session.</param>
    /// <param name="e">The event data.</param>
    private void OnSessionChanged(object? sender, PropertySessionChangedEventArgs e)
    {
        if (e.Kind == PropertySessionChangeKind.ErrorChanged || _disposed)
        {
            return;
        }

        Changed?.Invoke(this, EventArgs.Empty);
    }
}
