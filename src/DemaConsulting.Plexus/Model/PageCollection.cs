using System.Collections;
using System.Collections.ObjectModel;
using System.Collections.Specialized;

namespace DemaConsulting.Plexus;

/// <summary>
///     The ordered pages of a <see cref="Diagram"/>, with undoable add, insert, remove and reorder operations.
/// </summary>
/// <remarks>
///     Implements <see cref="INotifyCollectionChanged"/> and the non-generic read-only <see cref="IList"/>,
///     so UI item controls (for example an Avalonia <c>ItemsSource</c>) can bind to it directly without a
///     mirror collection; the <see cref="IList"/> mutators throw <see cref="NotSupportedException"/>
///     because pages are changed through <see cref="Add"/>, <see cref="Insert"/>, <see cref="Remove"/>
///     and <see cref="Move"/>, which are undoable. Removing a page detaches it (and everything on it)
///     from the diagram; undo re-attaches the very same page object with all its content.
///     <para>
///         Ordering guarantee: <see cref="CollectionChanged"/> and <see cref="Diagram.Changed"/> are raised
///         after the change is complete. For a removal that means the page is already out of the
///         collection, <see cref="DiagramPage.IsAttached"/> is <see langword="false"/> and the page's elements are no
///         longer found by <see cref="Diagram.FindElement"/>; for an addition the page is already attached.
///     </para>
///     Not thread-safe.
/// </remarks>
/// <example>
///     Add, reorder, rename and remove pages, and observe the list as a UI would.
///     <code>
///     using DemaConsulting.Plexus;
///
///     var diagram = new Diagram(types);                    // types: the registry of the DemaConsulting.Plexus example
///     diagram.Pages.CollectionChanged += (_, e) =&gt; Console.WriteLine($"pages: {e.Action}");
///
///     var first = diagram.Pages.Add("Overview");
///     var second = diagram.Pages.Add("Details");
///     diagram.Pages.Insert(0, "Cover");                    // at a position
///     diagram.Pages.Move(second, 0);                       // reorder
///     second.Name = "Details (draft)";                     // renaming a page is undoable too
///
///     foreach (var page in diagram.Pages)                  // in display order
///     {
///         Console.WriteLine($"{diagram.Pages.IndexOf(page)}: {page.Name}");
///     }
///
///     diagram.Pages.Remove(first);                         // detaches the page with all its content ...
///     diagram.History.Undo();                              // ... and undo re-attaches the very same page object
///     </code>
/// </example>
public sealed class PageCollection : IReadOnlyList<DiagramPage>, IList, INotifyCollectionChanged
{
    /// <summary>
    ///     The diagram that owns the pages.
    /// </summary>
    private readonly Diagram _diagram;

    /// <summary>
    ///     The pages in display order.
    /// </summary>
    private readonly ObservableCollection<DiagramPage> _pages = [];

    /// <summary>
    ///     Initializes a new instance of the <see cref="PageCollection"/> class.
    /// </summary>
    /// <param name="diagram">The owning diagram.</param>
    internal PageCollection(Diagram diagram)
    {
        _diagram = diagram;

        // Re-raise with this collection as sender so consumers see a stable event source
        _pages.CollectionChanged += (_, e) => CollectionChanged?.Invoke(this, e);
    }

    /// <summary>
    ///     Occurs after pages were added, removed or reordered, with the standard collection-changed details, so UI item controls bound to the collection stay in sync.
    /// </summary>
    /// <remarks>
    ///     The sender is this collection. A page rename does not raise this event; listen to
    ///     <see cref="DiagramPage.PropertyChanged"/> (or <see cref="Diagram.Changed"/>) to refresh page titles.
    /// </remarks>
    public event NotifyCollectionChangedEventHandler? CollectionChanged;

    /// <summary>
    ///     Gets the number of pages in the diagram.
    /// </summary>
    public int Count => _pages.Count;

    /// <summary>
    ///     Gets the page at the specified zero-based position.
    /// </summary>
    /// <param name="index">The zero-based page position.</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="index"/> is outside the collection.</exception>
    public DiagramPage this[int index] => _pages[index];

    /// <summary>
    ///     Adds a new, empty page at the end (undoable).
    /// </summary>
    /// <param name="name">The page name.</param>
    /// <returns>The new page.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="name"/> is null, empty or whitespace.</exception>
    public DiagramPage Add(string name) => Insert(_pages.Count, name);

    /// <summary>
    ///     Inserts a new, empty page at the given position (undoable).
    /// </summary>
    /// <param name="index">The position, from 0 to <see cref="Count"/>.</param>
    /// <param name="name">The page name.</param>
    /// <returns>The new page.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="name"/> is null, empty or whitespace.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="index"/> is out of range.</exception>
    public DiagramPage Insert(int index, string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentOutOfRangeException.ThrowIfNegative(index);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(index, _pages.Count);

        var page = new DiagramPage(_diagram, name);
        _diagram.History.Execute("Add page", () => InsertCore(index, page), () => RemoveCore(page));
        return page;
    }

    /// <summary>
    ///     Removes a page and its content from the diagram (undoable).
    /// </summary>
    /// <param name="page">The page to remove.</param>
    /// <returns><see langword="true"/> when the page was in the diagram and was removed.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="page"/> is null.</exception>
    public bool Remove(DiagramPage page)
    {
        ArgumentNullException.ThrowIfNull(page);
        var index = _pages.IndexOf(page);
        if (index < 0)
        {
            return false;
        }

        _diagram.History.Execute("Remove page", () => RemoveCore(page), () => InsertCore(index, page));
        return true;
    }

    /// <summary>
    ///     Moves a page to a new position (undoable).
    /// </summary>
    /// <param name="page">The page to move.</param>
    /// <param name="newIndex">The position the page should have afterwards.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="page"/> is null.</exception>
    /// <exception cref="ArgumentException">Thrown when the page is not in this collection.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="newIndex"/> is out of range.</exception>
    public void Move(DiagramPage page, int newIndex)
    {
        ArgumentNullException.ThrowIfNull(page);
        var oldIndex = _pages.IndexOf(page);
        if (oldIndex < 0)
        {
            throw new ArgumentException("The page is not in this diagram.", nameof(page));
        }

        ArgumentOutOfRangeException.ThrowIfNegative(newIndex);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(newIndex, _pages.Count);
        if (oldIndex == newIndex)
        {
            return;
        }

        _diagram.History.Execute("Move page", () => MoveCore(page, newIndex), () => MoveCore(page, oldIndex));
    }

    /// <summary>
    ///     Gets the position of a page.
    /// </summary>
    /// <param name="page">The page to find.</param>
    /// <returns>The zero-based index, or -1 when the page is not in the collection.</returns>
    public int IndexOf(DiagramPage page) => _pages.IndexOf(page);

    /// <summary>
    ///     Determines whether a page is in the collection.
    /// </summary>
    /// <param name="page">The page to find.</param>
    /// <returns><see langword="true"/> when the page is in the collection.</returns>
    public bool Contains(DiagramPage page) => _pages.Contains(page);

    /// <summary>
    ///     Returns an enumerator over the pages in display order.
    /// </summary>
    /// <returns>An enumerator; do not add or remove pages while enumerating.</returns>
    public IEnumerator<DiagramPage> GetEnumerator() => _pages.GetEnumerator();

    /// <inheritdoc />
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    /// <inheritdoc />
    bool IList.IsReadOnly => true;

    /// <inheritdoc />
    bool IList.IsFixedSize => true;

    /// <inheritdoc />
    bool ICollection.IsSynchronized => false;

    /// <inheritdoc />
    object ICollection.SyncRoot => ((ICollection)_pages).SyncRoot;

    /// <inheritdoc />
    /// <exception cref="NotSupportedException">Thrown when set; use <see cref="Insert"/> or <see cref="Move"/>.</exception>
    object? IList.this[int index]
    {
        get => _pages[index];
        set => throw ReadOnlyList();
    }

    /// <inheritdoc />
    bool IList.Contains(object? value) => value is DiagramPage page && _pages.Contains(page);

    /// <inheritdoc />
    int IList.IndexOf(object? value) => value is DiagramPage page ? _pages.IndexOf(page) : -1;

    /// <inheritdoc />
    void ICollection.CopyTo(Array array, int index) => ((ICollection)_pages).CopyTo(array, index);

    /// <inheritdoc />
    /// <exception cref="NotSupportedException">Always; use <see cref="Add"/>.</exception>
    int IList.Add(object? value) => throw ReadOnlyList();

    /// <inheritdoc />
    /// <exception cref="NotSupportedException">Always; use <see cref="Insert"/>.</exception>
    void IList.Insert(int index, object? value) => throw ReadOnlyList();

    /// <inheritdoc />
    /// <exception cref="NotSupportedException">Always; use <see cref="Remove"/>.</exception>
    void IList.Remove(object? value) => throw ReadOnlyList();

    /// <inheritdoc />
    /// <exception cref="NotSupportedException">Always; use <see cref="Remove"/>.</exception>
    void IList.RemoveAt(int index) => throw ReadOnlyList();

    /// <inheritdoc />
    /// <exception cref="NotSupportedException">Always.</exception>
    void IList.Clear() => throw ReadOnlyList();

    /// <summary>
    ///     Creates the exception thrown by the non-generic mutators.
    /// </summary>
    /// <returns>A <see cref="NotSupportedException"/> that points at the undoable API.</returns>
    private static NotSupportedException ReadOnlyList() =>
        new("The page list is read-only through IList; use Add, Insert, Remove and Move on PageCollection.");

    /// <summary>
    ///     Attaches a page at an index and registers its content; used by commands.
    /// </summary>
    /// <param name="index">The insertion index.</param>
    /// <param name="page">The page to attach.</param>
    private void InsertCore(int index, DiagramPage page)
    {
        // The diagram's settings may have changed while the page was out of the list, so its routing caches start over
        page.InvalidateRoutingCaches();
        page.IsAttached = true;
        foreach (var element in page.Elements)
        {
            _diagram.RegisterElement(element);
        }

        _pages.Insert(index, page);
        _diagram.RaiseChanged(DiagramChangeKind.PageAdded, page, null, null);
    }

    /// <summary>
    ///     Detaches a page and unregisters its content; used by commands.
    /// </summary>
    /// <param name="page">The page to detach.</param>
    private void RemoveCore(DiagramPage page)
    {
        // Finish detaching before any notification so observers never see a half-attached page
        foreach (var element in page.Elements)
        {
            _diagram.UnregisterElement(element);
        }

        page.IsAttached = false;
        page.InvalidateRoutingCaches();
        _pages.Remove(page);
        _diagram.RaiseChanged(DiagramChangeKind.PageRemoved, page, null, null);
    }

    /// <summary>
    ///     Reorders a page; used by commands.
    /// </summary>
    /// <param name="page">The page to move.</param>
    /// <param name="newIndex">The new position.</param>
    private void MoveCore(DiagramPage page, int newIndex)
    {
        _pages.Move(_pages.IndexOf(page), newIndex);
        _diagram.RaiseChanged(DiagramChangeKind.PageMoved, page, null, null);
    }
}
