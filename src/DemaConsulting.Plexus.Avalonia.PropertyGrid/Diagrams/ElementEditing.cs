using System.ComponentModel;

namespace DemaConsulting.Plexus.Avalonia.PropertyGrid.Diagrams;

/// <summary>
///     The commit plumbing shared by the Plexus sources: runs the grid's writes and the source's publish step inside one
///     undo transaction, throws the working state away afterwards, and keeps the source in step with changes made to the
///     element from elsewhere (undo, redo, other editors).
/// </summary>
internal sealed class ElementEditing : IDisposable
{
    /// <summary>
    ///     The edited element.
    /// </summary>
    private readonly DiagramElement _element;

    /// <summary>
    ///     The diagram that owns the element.
    /// </summary>
    private readonly Diagram _diagram;

    /// <summary>
    ///     Copies the working state to the element (inside the transaction); throws to refuse the commit.
    /// </summary>
    private readonly Action _publish;

    /// <summary>
    ///     Throws away the working state of the source (besides <see cref="Work"/>).
    /// </summary>
    private readonly Action _discard;

    /// <summary>
    ///     The names of the element properties whose change makes the source re-read.
    /// </summary>
    private readonly HashSet<string> _watched;

    /// <summary>
    ///     Whether a commit is running (its own change notifications are ignored).
    /// </summary>
    private bool _running;

    /// <summary>
    ///     Initializes a new instance of the <see cref="ElementEditing"/> class.
    /// </summary>
    /// <param name="element">The edited element, which must be attached to a diagram.</param>
    /// <param name="watched">The names of the element properties whose change makes the source re-read.</param>
    /// <param name="publish">Copies the working state to the element inside the transaction; throws to refuse the commit.</param>
    /// <param name="discard">Throws away the source's own working state.</param>
    /// <exception cref="ArgumentException">The element is not attached to a diagram.</exception>
    internal ElementEditing(DiagramElement element, IEnumerable<string> watched, Action publish, Action discard)
    {
        ArgumentNullException.ThrowIfNull(element);
        _element = element;
        _diagram = element.Page is { IsAttached: true } page
            ? page.Diagram
            : throw new ArgumentException("The element is not attached to a diagram.", nameof(element));
        _watched = [.. watched];
        _publish = publish;
        _discard = discard;
        element.PropertyChanged += OnElementChanged;
    }

    /// <summary>
    ///     Occurs when the source must be re-read.
    /// </summary>
    internal event EventHandler<PropertySourceChangedEventArgs>? Changed;

    /// <summary>
    ///     Gets the diagram that owns the element.
    /// </summary>
    internal Diagram Diagram => _diagram;

    /// <summary>
    ///     Gets the values the grid wrote during the running commit.
    /// </summary>
    internal WorkingValues Work { get; } = new();

    /// <summary>
    ///     Runs one commit: the grid's writes and the publish step become one undo entry, or nothing changes.
    /// </summary>
    /// <param name="apply">The grid's writes.</param>
    internal void RunCommit(Action apply)
    {
        ArgumentNullException.ThrowIfNull(apply);
        Discard();
        _running = true;
        try
        {
            _diagram.History.Transact("Edit properties", () =>
            {
                apply();
                _publish();
            });
        }
        finally
        {
            _running = false;
            Discard();
            Changed?.Invoke(this, new PropertySourceChangedEventArgs());
        }
    }

    /// <summary>
    ///     Stops listening to the element.
    /// </summary>
    public void Dispose() => _element.PropertyChanged -= OnElementChanged;

    /// <summary>
    ///     Throws the working state away.
    /// </summary>
    private void Discard()
    {
        Work.Clear();
        _discard();
    }

    /// <summary>
    ///     Re-reads the element after a change that did not come from a commit of this source.
    /// </summary>
    /// <param name="sender">The element.</param>
    /// <param name="e">The change.</param>
    private void OnElementChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (_running || e.PropertyName is null || !_watched.Contains(e.PropertyName))
        {
            return;
        }

        Discard();
        Changed?.Invoke(this, new PropertySourceChangedEventArgs());
    }
}
