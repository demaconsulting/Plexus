using System.Collections.ObjectModel;
using System.ComponentModel;

namespace DemaConsulting.Plexus;

/// <summary>
///     One canvas of a <see cref="Diagram"/>: the nodes, wires, annotations and groups placed on it.
/// </summary>
/// <remarks>
///     Create pages with <see cref="PageCollection.Add"/>. All content is created, changed and
///     removed through the page, and every such operation is one undoable history entry. The content
///     lists (<see cref="Nodes"/>, <see cref="Wires"/>, <see cref="Annotations"/>, <see cref="Groups"/>,
///     <see cref="Elements"/>) are read-only and observable, so they can be bound to a UI directly.
///     Not thread-safe.
/// </remarks>
/// <example>
///     Build content through the page: add nodes by type key, connect ports by <c>"Node.port"</c>, move them, and
///     send them to another page. The example uses the <c>demo.Source</c> and <c>demo.Sink</c> node types of the
///     <c>DemaConsulting.Plexus</c> namespace example (<c>types</c> is that registry).
///     <code>
///     using DemaConsulting.Plexus;
///     using DemaConsulting.Plexus.Geometry;
///
///     var diagram = new Diagram(types);
///     var page = diagram.Pages.Add("Main");
///     var other = diagram.Pages.Add("Second");
///
///     // Add the nodes first, then connect: a new wire is routed around the nodes that exist at that moment.
///     var c = page.AddNode("demo.Source", 0, 120, name: "C");        // type key, position, then the name (generated when omitted)
///     var d = page.AddNode("demo.Sink", 180, 120, name: "D");
///
///     var check = page.CanConnect(c.Output("out"), d.Input("in"));   // ask first; Connect throws with the same reason
///     Console.WriteLine(check.IsAllowed ? "allowed" : check.Reason);
///     var wire = page.Connect("C.out", "D.in");                      // "NodeName.portName" on both ends
///
///     page.Move([c, d], 30, 0);                                      // delta in diagram units; wires between them follow
///     page.AddAnnotation("Remember me", new Rect(0, -60, 150, 45));  // a free-floating note
///
///     diagram.MoveToPage([c, d], other);                             // to another page as one undo entry; the wire
///                                                                    // between them travels along, wires to nodes
///                                                                    // left behind are removed
///     diagram.History.Undo();                                        // everything is back on "Main"
///
///     page.Remove(wire);                                             // removing a wire leaves its nodes; removing a node takes its wires
///     </code>
/// </example>
public sealed partial class DiagramPage : INotifyPropertyChanged
{
    /// <summary>
    ///     Mutable list of nodes.
    /// </summary>
    private readonly ObservableCollection<Node> _nodes = [];

    /// <summary>
    ///     Mutable list of wires.
    /// </summary>
    private readonly ObservableCollection<Wire> _wires = [];

    /// <summary>
    ///     Mutable list of annotations.
    /// </summary>
    private readonly ObservableCollection<Annotation> _annotations = [];

    /// <summary>
    ///     Mutable list of groups.
    /// </summary>
    private readonly ObservableCollection<Group> _groups = [];

    /// <summary>
    ///     Mutable list of all elements in z-order (first is bottom-most).
    /// </summary>
    private readonly ObservableCollection<DiagramElement> _elements = [];

    /// <summary>
    ///     The page name.
    /// </summary>
    private string _name;

    /// <summary>
    ///     The routing obstacles of the current layout, or null when they must be rebuilt.
    /// </summary>
    private ReadOnlyCollection<Geometry.Rect>? _obstacleCache;

    /// <summary>
    ///     The prepared group frames of the current layout, or null when they must be rebuilt.
    /// </summary>
    private GroupObstacles? _groupCache;

    /// <summary>
    ///     The traffic of every visible wire, in page order, for routing new wires, or null when it must be rebuilt.
    /// </summary>
    private WireTraffic? _trafficCache;

    /// <summary>
    ///     Initializes a new instance of the <see cref="DiagramPage"/> class.
    /// </summary>
    /// <param name="diagram">The owning diagram.</param>
    /// <param name="name">The page name.</param>
    internal DiagramPage(Diagram diagram, string name)
    {
        Diagram = diagram;
        _name = name;
        Nodes = new ReadOnlyObservableCollection<Node>(_nodes);
        Wires = new ReadOnlyObservableCollection<Wire>(_wires);
        Annotations = new ReadOnlyObservableCollection<Annotation>(_annotations);
        Groups = new ReadOnlyObservableCollection<Group>(_groups);
        Elements = new ReadOnlyObservableCollection<DiagramElement>(_elements);
    }

    /// <summary>
    ///     Occurs after the page's <see cref="Name"/> changed, including by undo and redo.
    /// </summary>
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>
    ///     Gets the diagram the page was created in.
    /// </summary>
    public Diagram Diagram { get; }

    /// <summary>
    ///     Gets a value indicating whether the page is currently part of <see cref="Diagram"/>'s page list.
    /// </summary>
    /// <remarks>
    ///     A removed page keeps its content but cannot be modified until an undo re-attaches it.
    /// </remarks>
    public bool IsAttached { get; internal set; }

    /// <summary>
    ///     Gets or sets the page name (undoable).
    /// </summary>
    /// <exception cref="ArgumentException">Thrown when set to null, empty or whitespace.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the page is removed from its diagram.</exception>
    public string Name
    {
        get => _name;
        set
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(value);
            EnsureAttached();
            if (string.Equals(_name, value, StringComparison.Ordinal))
            {
                return;
            }

            Diagram.History.Execute(new SetValueCommand<string>(
                "Rename page",
                this,
                nameof(Name),
                _name,
                value,
                v =>
                {
                    _name = v;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Name)));
                    Diagram.RaiseChanged(DiagramChangeKind.PageRenamed, this, null, nameof(Name));
                }));
        }
    }

    /// <summary>
    ///     Gets the nodes in the order they were added.
    /// </summary>
    public ReadOnlyObservableCollection<Node> Nodes { get; }

    /// <summary>
    ///     Gets the wires in the order they were added.
    /// </summary>
    public ReadOnlyObservableCollection<Wire> Wires { get; }

    /// <summary>
    ///     Gets the annotations in the order they were added.
    /// </summary>
    public ReadOnlyObservableCollection<Annotation> Annotations { get; }

    /// <summary>
    ///     Gets the groups in the order they were created.
    /// </summary>
    public ReadOnlyObservableCollection<Group> Groups { get; }

    /// <summary>
    ///     Gets every element of the page in z-order (first is bottom-most).
    /// </summary>
    public ReadOnlyObservableCollection<DiagramElement> Elements { get; }

    /// <summary>
    ///     Inserts an element into the page's lists and registers it; used by commands.
    /// </summary>
    /// <param name="element">The element to insert.</param>
    /// <param name="typedIndex">Position in the element's typed list, or -1 to append.</param>
    /// <param name="zIndex">Position in the z-ordered list, or -1 to append.</param>
    /// <returns>The positions actually used.</returns>
    internal (int TypedIndex, int ZIndex) AttachCore(DiagramElement element, int typedIndex, int zIndex)
    {
        // Finish attaching before any notification so observers see a fully attached element, and bring the
        // routing caches up to date first so an observer that routes a wire sees the new element
        element.Page = this;
        if (IsAttached)
        {
            Diagram.RegisterElement(element);
        }

        if (element is Wire appended && (typedIndex < 0 || typedIndex == _wires.Count))
        {
            AppendTraffic(appended);
        }
        else
        {
            OnElementChanging(element);
        }

        var typed = element switch
        {
            Node node => InsertAt(_nodes, node, typedIndex),
            Wire wire => InsertAt(_wires, wire, typedIndex),
            Annotation annotation => InsertAt(_annotations, annotation, typedIndex),
            Group group => InsertAt(_groups, group, typedIndex),
            _ => throw new InvalidOperationException("Unsupported element type."),
        };

        var z = InsertAt(_elements, element, zIndex);
        Diagram.RaiseChanged(DiagramChangeKind.ElementAdded, this, element, null);
        return (typed, z);
    }

    /// <summary>
    ///     Removes an element from the page's lists and unregisters it; used by commands.
    /// </summary>
    /// <remarks>
    ///     The element is fully detached (<see cref="DiagramElement.Page"/> is null and it is no longer
    ///     found by <see cref="Diagram.FindElement"/>) before any collection or <see cref="Diagram.Changed"/>
    ///     notification is raised, so observers never see a half-removed element.
    /// </remarks>
    /// <param name="element">The element to remove.</param>
    /// <returns>The positions the element had.</returns>
    internal (int TypedIndex, int ZIndex) DetachCore(DiagramElement element)
    {
        if (IsAttached)
        {
            Diagram.UnregisterElement(element);
        }

        OnElementChanging(element);
        element.Page = null;
        var typed = element switch
        {
            Node node => RemoveFrom(_nodes, node),
            Wire wire => RemoveFrom(_wires, wire),
            Annotation annotation => RemoveFrom(_annotations, annotation),
            Group group => RemoveFrom(_groups, group),
            _ => throw new InvalidOperationException("Unsupported element type."),
        };

        var z = RemoveFrom(_elements, element);
        Diagram.RaiseChanged(DiagramChangeKind.ElementRemoved, this, element, null);
        return (typed, z);
    }

    /// <summary>
    ///     Throws when the page is not part of its diagram.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when the page was removed.</exception>
    internal void EnsureAttached()
    {
        if (!IsAttached)
        {
            throw new InvalidOperationException("The page is not part of the diagram; undo its removal before modifying it.");
        }
    }

    /// <summary>
    ///     Inserts an item at an index, or appends when the index is negative.
    /// </summary>
    /// <typeparam name="T">The item type.</typeparam>
    /// <param name="list">The list to change.</param>
    /// <param name="item">The item to insert.</param>
    /// <param name="index">The position, or a negative value to append.</param>
    /// <returns>The position used.</returns>
    private static int InsertAt<T>(ObservableCollection<T> list, T item, int index)
    {
        var position = index < 0 ? list.Count : index;
        list.Insert(position, item);
        return position;
    }

    /// <summary>
    ///     Removes an item and reports the position it had.
    /// </summary>
    /// <typeparam name="T">The item type.</typeparam>
    /// <param name="list">The list to change.</param>
    /// <param name="item">The item to remove.</param>
    /// <returns>The position the item had.</returns>
    private static int RemoveFrom<T>(ObservableCollection<T> list, T item)
    {
        var position = list.IndexOf(item);
        list.RemoveAt(position);
        return position;
    }
}
