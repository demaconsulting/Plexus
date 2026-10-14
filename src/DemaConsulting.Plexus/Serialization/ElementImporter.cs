using System.Globalization;

namespace DemaConsulting.Plexus;

/// <summary>
///     Validates snapshot content and turns it into detached model elements; the single "write" half of
///     the clone path used by snapshot loading, paste and duplicate.
/// </summary>
/// <remarks>
///     All validation happens while constructing detached elements, so a failure can never leave the
///     target page or diagram partly modified. The input may be untrusted (clipboard text), therefore
///     every number, reference and nesting level is checked before use and problems are reported as
///     messages rather than thrown to the caller.
/// </remarks>
internal static class ElementImporter
{
    /// <summary>
    ///     Largest absolute coordinate, size or offset accepted; keeps later arithmetic finite and rejects absurd geometry.
    /// </summary>
    public const double MaxCoordinate = 1e9;

    /// <summary>
    ///     Deepest group nesting accepted; also guards against membership cycles.
    /// </summary>
    private const int MaxGroupDepth = 64;

    /// <summary>
    ///     Finds every node type key of the content that the registry does not contain.
    /// </summary>
    /// <param name="nodes">The node snapshots.</param>
    /// <param name="types">The registry to check against.</param>
    /// <returns>The distinct missing keys in first-seen order.</returns>
    public static List<string> FindMissingTypes(IEnumerable<NodeSnapshot?> nodes, NodeTypeRegistry types)
    {
        var missing = new List<string>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var node in nodes)
        {
            var key = node?.TypeKey;
            if (key is not null && !types.Contains(key) && seen.Add(key))
            {
                missing.Add(key);
            }
        }

        return missing;
    }

    /// <summary>
    ///     Validates content and builds detached elements for a target page.
    /// </summary>
    /// <param name="target">The page the elements are meant for (read only here).</param>
    /// <param name="content">The content to import.</param>
    /// <param name="options">How to import.</param>
    /// <returns>The prepared import; check <see cref="PreparedImport.Success"/> before applying.</returns>
    public static PreparedImport Prepare(DiagramPage target, ElementContent content, ImportOptions options)
    {
        var result = new PreparedImport();
        try
        {
            new Builder(target, content, options, result).Build();
        }
        catch (ImportException ex)
        {
            result.Fail(ex.Message, ex.MissingNodeTypes);
        }

        return result;
    }

    /// <summary>
    ///     Signals a validation problem inside the builder; never escapes <see cref="Prepare"/>.
    /// </summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Major Code Smell", "S3871:Exception types should be \"public\"", Justification = "Private control-flow exception that never escapes ElementImporter.Prepare.")]
    private sealed class ImportException : Exception
    {
        /// <summary>
        ///     Initializes a new instance of the <see cref="ImportException"/> class.
        /// </summary>
        /// <param name="message">The problem description.</param>
        /// <param name="missingNodeTypes">Missing node types when that is the problem, otherwise null.</param>
        public ImportException(string message, IReadOnlyList<string>? missingNodeTypes = null)
            : base(message) => MissingNodeTypes = missingNodeTypes;

        /// <summary>
        ///     Gets the missing node type keys, or null.
        /// </summary>
        public IReadOnlyList<string>? MissingNodeTypes { get; }
    }

    /// <summary>
    ///     Holds the state of one import while elements are validated and built.
    /// </summary>
    private sealed class Builder
    {
        /// <summary>
        ///     The page the elements are meant for.
        /// </summary>
        private readonly DiagramPage _target;

        /// <summary>
        ///     The content being imported.
        /// </summary>
        private readonly ElementContent _content;

        /// <summary>
        ///     How to import.
        /// </summary>
        private readonly ImportOptions _options;

        /// <summary>
        ///     Receives elements and findings.
        /// </summary>
        private readonly PreparedImport _result;

        /// <summary>
        ///     Source ids seen so far, for uniqueness.
        /// </summary>
        private readonly HashSet<string> _sourceIds = new(StringComparer.Ordinal);

        /// <summary>
        ///     Created nodes, groups and annotations by source id (wires are never referenced).
        /// </summary>
        private readonly Dictionary<string, DiagramElement> _referenceable = new(StringComparer.Ordinal);

        /// <summary>
        ///     Every created element by source id.
        /// </summary>
        private readonly Dictionary<string, DiagramElement> _all = new(StringComparer.Ordinal);

        /// <summary>
        ///     Created nodes by source id.
        /// </summary>
        private readonly Dictionary<string, Node> _nodes = new(StringComparer.Ordinal);

        /// <summary>
        ///     Port pairs already connected by a created wire.
        /// </summary>
        private readonly HashSet<(Port Source, Port Target)> _wirePairs = [];

        /// <summary>
        ///     Names already taken, built lazily when names must be made unique.
        /// </summary>
        private HashSet<string>? _usedNames;

        /// <summary>
        ///     Horizontal translation applied to all geometry.
        /// </summary>
        private double _dx;

        /// <summary>
        ///     Vertical translation applied to all geometry.
        /// </summary>
        private double _dy;

        /// <summary>
        ///     Initializes a new instance of the <see cref="Builder"/> class.
        /// </summary>
        /// <param name="target">The target page.</param>
        /// <param name="content">The content to import.</param>
        /// <param name="options">How to import.</param>
        /// <param name="result">Receives elements and findings.</param>
        public Builder(DiagramPage target, ElementContent content, ImportOptions options, PreparedImport result)
        {
            _target = target;
            _content = content;
            _options = options;
            _result = result;
        }

        /// <summary>
        ///     Runs validation and construction.
        /// </summary>
        /// <exception cref="ImportException">Thrown on the first problem found.</exception>
        public void Build()
        {
            IReadOnlyList<NodeSnapshot> nodes = _content.Nodes ?? [];
            IReadOnlyList<WireSnapshot> wires = _content.Wires ?? [];
            IReadOnlyList<AnnotationSnapshot> annotations = _content.Annotations ?? [];
            IReadOnlyList<GroupSnapshot> groups = _content.Groups ?? [];

            // Refuse oversized input before doing any per-element work
            var total = (long)nodes.Count + wires.Count + annotations.Count + groups.Count;
            if (total > _options.MaxElements)
            {
                throw new ImportException($"The content has {total} elements; at most {_options.MaxElements} are accepted.");
            }

            // Report all unknown node types together so the user can fix them in one go
            var missing = FindMissingTypes(nodes, _target.Diagram.Types);
            if (missing.Count > 0)
            {
                throw new ImportException($"Unknown node type(s): {string.Join(", ", missing)}.", missing);
            }

            // Geometry is validated up front because the paste offset depends on the content's bounds
            var nodeBounds = nodes.Select(n => ReadRect(Require(n, "node").Bounds, "node bounds")).ToList();
            var annotationBounds = annotations.Select(a => ReadRect(Require(a, "annotation").Bounds, "annotation bounds")).ToList();
            var collapsedPositions = groups.Select(g => ReadPoint(Require(g, "group").CollapsedPosition, "group position")).ToList();
            ComputeOffset(nodeBounds, annotationBounds, collapsedPositions);

            var createdNodes = nodes.Select((n, i) => CreateNode(n, nodeBounds[i])).ToList();
            var createdAnnotations = annotations.Select((a, i) => CreateAnnotation(a, annotationBounds[i])).ToList();
            var createdGroups = groups.Select(CreateGroup).ToList();
            LinkGroups(groups, createdGroups, collapsedPositions);
            var createdWires = wires.Select(CreateWire).ToList();
            OrderElements(createdGroups, createdNodes, createdAnnotations, createdWires);
        }

        /// <summary>
        ///     Throws when a list entry is null (possible with untrusted JSON such as <c>[null]</c>).
        /// </summary>
        /// <typeparam name="T">The entry type.</typeparam>
        /// <param name="item">The entry.</param>
        /// <param name="what">What the entry should be, for the message.</param>
        /// <returns>The entry.</returns>
        private static T Require<T>(T? item, string what)
            where T : class => item ?? throw new ImportException($"The content contains an empty {what} entry.");

        /// <summary>
        ///     Validates a stored rectangle.
        /// </summary>
        /// <param name="rect">The stored rectangle.</param>
        /// <param name="what">What it describes, for the message.</param>
        /// <returns>The rectangle.</returns>
        private static Rect ReadRect(RectSnapshot? rect, string what)
        {
            var r = rect ?? throw new ImportException($"A {what} value is missing.");
            if (!IsCoordinate(r.X) || !IsCoordinate(r.Y) || !IsCoordinate(r.Width) || !IsCoordinate(r.Height) || r.Width < 0 || r.Height < 0)
            {
                throw new ImportException($"A {what} value is invalid (finite numbers within ±1e9 and a non-negative size are required).");
            }

            return r.ToRect();
        }

        /// <summary>
        ///     Validates a stored point.
        /// </summary>
        /// <param name="point">The stored point.</param>
        /// <param name="what">What it describes, for the message.</param>
        /// <returns>The point.</returns>
        private static Point ReadPoint(PointSnapshot? point, string what)
        {
            var p = point ?? throw new ImportException($"A {what} value is missing.");
            if (!IsCoordinate(p.X) || !IsCoordinate(p.Y))
            {
                throw new ImportException($"A {what} value is invalid (finite numbers within ±1e9 are required).");
            }

            return p.ToPoint();
        }

        /// <summary>
        ///     Determines whether a number is an acceptable coordinate.
        /// </summary>
        /// <param name="value">The number.</param>
        /// <returns><see langword="true"/> when finite and within the accepted range.</returns>
        private static bool IsCoordinate(double value) => double.IsFinite(value) && Math.Abs(value) <= MaxCoordinate;

        /// <summary>
        ///     Removes trailing digits so numbered names continue the sequence (Axis1 becomes Axis, then Axis2).
        /// </summary>
        /// <param name="name">The name.</param>
        /// <returns>The name without trailing digits, or the whole name when nothing else would remain.</returns>
        private static string StripTrailingDigits(string name)
        {
            var end = name.Length;
            while (end > 0 && char.IsAsciiDigit(name[end - 1]))
            {
                end--;
            }

            return end == 0 ? name : name[..end];
        }

        /// <summary>
        ///     Determines the translation applied to all geometry.
        /// </summary>
        /// <param name="nodeBounds">Node boxes.</param>
        /// <param name="annotationBounds">Annotation boxes.</param>
        /// <param name="collapsedPositions">Group collapsed positions.</param>
        private void ComputeOffset(List<Rect> nodeBounds, List<Rect> annotationBounds, List<Point> collapsedPositions)
        {
            if (_options.Restore)
            {
                return;
            }

            if (_options.At is not { } at)
            {
                _dx = _options.Dx;
                _dy = _options.Dy;
                return;
            }

            // The pasted content's bounds are the union of node and annotation boxes and collapsed group boxes
            var theme = _target.Diagram.Theme;
            var boxes = nodeBounds
                .Concat(annotationBounds)
                .Concat(collapsedPositions.Select(p => new Rect(p.X, p.Y, theme.CollapsedGroupWidth, theme.CollapsedGroupHeight)))
                .ToList();
            if (boxes.Count == 0)
            {
                return;
            }

            _dx = at.X - boxes.Min(b => b.Left);
            _dy = at.Y - boxes.Min(b => b.Top);
        }

        /// <summary>
        ///     Chooses the id an element will carry.
        /// </summary>
        /// <param name="sourceId">The id in the content.</param>
        /// <param name="what">What the element is, for messages.</param>
        /// <returns>The source id when restoring, a fresh id when cloning.</returns>
        private string ClaimId(string? sourceId, string what)
        {
            if (string.IsNullOrWhiteSpace(sourceId))
            {
                throw new ImportException($"A {what} has no id.");
            }

            if (!_sourceIds.Add(sourceId))
            {
                throw new ImportException($"The element id '{sourceId}' is used more than once.");
            }

            if (!_options.Restore)
            {
                return Guid.NewGuid().ToString("N");
            }

            if (_target.Diagram.FindElement(sourceId) is not null)
            {
                throw new ImportException($"The element id '{sourceId}' already exists in the diagram.");
            }

            return sourceId;
        }

        /// <summary>
        ///     Chooses the name a node will carry.
        /// </summary>
        /// <param name="proposed">The name in the content.</param>
        /// <returns>The trimmed name, made unique by a numeric suffix when cloning into a diagram that requires unique names.</returns>
        private string ResolveName(string? proposed)
        {
            var name = proposed?.Trim();
            if (string.IsNullOrEmpty(name))
            {
                throw new ImportException("A node has an empty name.");
            }

            if (_options.Restore || !_target.Diagram.RequireUniqueNodeNames)
            {
                return name;
            }

            _usedNames ??= new HashSet<string>(_target.Diagram.AllNodes().Select(n => n.Name), StringComparer.Ordinal);
            var unique = name;
            if (_usedNames.Contains(unique))
            {
                // Continue the numbering: Axis, Axis1, Axis2 ...
                var stem = StripTrailingDigits(name);
                var number = 1;
                do
                {
                    unique = stem + number.ToString(CultureInfo.InvariantCulture);
                    number++;
                }
                while (_usedNames.Contains(unique));
            }

            _usedNames.Add(unique);
            return unique;
        }

        /// <summary>
        ///     Builds a detached node.
        /// </summary>
        /// <param name="snapshot">The node snapshot.</param>
        /// <param name="bounds">The validated source bounds.</param>
        /// <returns>The node.</returns>
        private Node CreateNode(NodeSnapshot snapshot, Rect bounds)
        {
            var id = ClaimId(snapshot.Id, "node");
            var type = _target.Diagram.Types.Get(snapshot.TypeKey);
            var name = ResolveName(snapshot.Name);
            var node = new Node(id, type, name, bounds.Offset(_dx, _dy));
            LoadProperties(node, snapshot);
            LoadData(node, snapshot);

            _referenceable[snapshot.Id] = node;
            _all[snapshot.Id] = node;
            _nodes[snapshot.Id] = node;
            return node;
        }

        /// <summary>
        ///     Loads the stored property values into a detached node.
        /// </summary>
        /// <param name="node">The node.</param>
        /// <param name="snapshot">The node snapshot.</param>
        private void LoadProperties(Node node, NodeSnapshot snapshot)
        {
            foreach (var (name, stored) in snapshot.Properties ?? new Dictionary<string, PropertySnapshot>())
            {
                if (string.IsNullOrEmpty(name) || stored is null)
                {
                    throw new ImportException($"Node '{node.Name}' has an invalid property entry.");
                }

                // A value that is not even valid for its own stated kind means the document is corrupt
                if (!PropertyCodec.TryRead(stored, out var value, out var problem))
                {
                    throw new ImportException($"Property '{name}' of node '{node.Name}' is malformed: {problem}");
                }

                var definition = node.Type.FindProperty(name);
                if (definition is not null && !TryCoerce(definition, ref value))
                {
                    // The target defines the property differently: restoring is strict, cloning keeps the default
                    var message = $"Property '{name}' of node '{node.Name}' holds a {stored.Kind} value that does not fit the {definition.ValueKind} definition";
                    if (_options.Restore)
                    {
                        throw new ImportException(message + ".");
                    }

                    _result.Warn(message + "; the default value was used.");
                    continue;
                }

                node.Properties.LoadValue(name, value);
            }
        }

        /// <summary>
        ///     Coerces a value to a declared property's kind.
        /// </summary>
        /// <param name="definition">The declaration.</param>
        /// <param name="value">The value; replaced by its canonical form on success.</param>
        /// <returns><see langword="true"/> when the value fits the declaration.</returns>
        private static bool TryCoerce(PropertyDefinition definition, ref object value)
        {
            try
            {
                value = PropertyValues.Coerce(value, definition.ValueKind, definition.Choices);
                return true;
            }
            catch (ArgumentException)
            {
                return false;
            }
        }

        /// <summary>
        ///     Loads the stored payload (or the carried reference) into a detached node.
        /// </summary>
        /// <param name="node">The node.</param>
        /// <param name="snapshot">The node snapshot.</param>
        private void LoadData(Node node, NodeSnapshot snapshot)
        {
            if (snapshot.Data is null)
            {
                // Only in-process duplicates carry a reference; it is shared, as documented
                node.InitializeData(snapshot.DataReference);
                return;
            }

            if (!NodeDataCodec.CanSerialize(node.Type))
            {
                // The target type has no way to read a payload, so it cannot be restored
                _result.Warn($"The data of node '{node.Name}' was ignored because node type '{node.Type.Key}' declares no data type or serializer.");
                return;
            }

            if (NodeDataCodec.TryDeserialize(node.Type, snapshot.Data, out var data, out var problem))
            {
                node.InitializeData(data);
                return;
            }

            var message = $"The data of node '{node.Name}' could not be read: {problem}";
            if (_options.Restore)
            {
                throw new ImportException(message);
            }

            _result.Warn(message + "; the node was pasted without data.");
        }

        /// <summary>
        ///     Builds a detached annotation.
        /// </summary>
        /// <param name="snapshot">The annotation snapshot.</param>
        /// <param name="bounds">The validated source bounds.</param>
        /// <returns>The annotation.</returns>
        private Annotation CreateAnnotation(AnnotationSnapshot snapshot, Rect bounds)
        {
            var id = ClaimId(snapshot.Id, "annotation");
            var annotation = new Annotation(id, snapshot.Text ?? string.Empty, bounds.Offset(_dx, _dy));
            if (snapshot.Fill is not null)
            {
                if (!Color.TryParse(snapshot.Fill, out var fill))
                {
                    throw new ImportException($"The fill '{snapshot.Fill}' of an annotation is not a valid color.");
                }

                annotation.InitializeFill(fill);
            }

            _referenceable[snapshot.Id] = annotation;
            _all[snapshot.Id] = annotation;
            return annotation;
        }

        /// <summary>
        ///     Builds a detached, still empty group.
        /// </summary>
        /// <param name="snapshot">The group snapshot.</param>
        /// <returns>The group.</returns>
        private Group CreateGroup(GroupSnapshot snapshot)
        {
            var id = ClaimId(snapshot.Id, "group");
            var group = new Group(id, snapshot.Title ?? string.Empty);
            _referenceable[snapshot.Id] = group;
            _all[snapshot.Id] = group;
            return group;
        }

        /// <summary>
        ///     Establishes membership, nesting and collapsed state, after checking for sharing, cycles and excessive depth.
        /// </summary>
        /// <param name="snapshots">The group snapshots.</param>
        /// <param name="groups">The created groups, parallel to <paramref name="snapshots"/>.</param>
        /// <param name="collapsedPositions">The validated source collapsed positions, parallel to <paramref name="snapshots"/>.</param>
        private void LinkGroups(IReadOnlyList<GroupSnapshot> snapshots, List<Group> groups, List<Point> collapsedPositions)
        {
            // First pass: resolve every member reference and record who owns whom, changing nothing yet
            var owner = new Dictionary<DiagramElement, Group>();
            var memberLists = new List<List<DiagramElement>>();
            for (var i = 0; i < snapshots.Count; i++)
            {
                var members = new List<DiagramElement>();
                foreach (var memberId in snapshots[i].MemberIds ?? [])
                {
                    if (memberId is null || !_referenceable.TryGetValue(memberId, out var member))
                    {
                        throw new ImportException($"Group '{snapshots[i].Title}' refers to an unknown member '{memberId}'.");
                    }

                    if (!owner.TryAdd(member, groups[i]))
                    {
                        throw new ImportException($"The element '{memberId}' belongs to more than one group.");
                    }

                    members.Add(member);
                }

                memberLists.Add(members);
            }

            // A cycle never terminates and excessive depth would stress recursive model code, so bound the walk
            foreach (var group in groups)
            {
                var depth = 0;
                DiagramElement current = group;
                while (owner.TryGetValue(current, out var parent))
                {
                    if (++depth > MaxGroupDepth)
                    {
                        throw new ImportException($"Groups are nested deeper than {MaxGroupDepth} levels or contain a cycle.");
                    }

                    current = parent;
                }
            }

            // Second pass: the structure is sound, so wire it up
            for (var i = 0; i < groups.Count; i++)
            {
                foreach (var member in memberLists[i])
                {
                    groups[i].InsertMemberCore(-1, member);
                }

                var position = collapsedPositions[i];
                groups[i].SetCollapsedCore(snapshots[i].IsCollapsed, new Point(position.X + _dx, position.Y + _dy));
            }
        }

        /// <summary>
        ///     Builds a detached wire between created nodes.
        /// </summary>
        /// <param name="snapshot">The wire snapshot.</param>
        /// <returns>The wire.</returns>
        private Wire CreateWire(WireSnapshot snapshot)
        {
            Require(snapshot, "wire");
            var id = ClaimId(snapshot.Id, "wire");
            var source = FindPort(snapshot.SourceNodeId, snapshot.SourcePort, PortDirection.Output);
            var target = FindPort(snapshot.TargetNodeId, snapshot.TargetPort, PortDirection.Input);
            if (ReferenceEquals(source.Node, target.Node))
            {
                throw new ImportException($"The wire '{snapshot.Id}' connects node '{source.Node.Name}' to itself.");
            }

            // Stored routes move with their nodes; every wire here has both ends in the same import
            var route = (snapshot.Route ?? [])
                .Select(p => ReadPoint(p, "wire route"))
                .Select(p => new Point(p.X + _dx, p.Y + _dy));
            var wire = new Wire(id, source, target, route);

            // Two wires between the same ports would be rejected by Connect, so reject them here too
            if (!_wirePairs.Add((source, target)))
            {
                throw new ImportException($"More than one wire connects '{source}' to '{target}'.");
            }

            _all[snapshot.Id] = wire;
            return wire;
        }

        /// <summary>
        ///     Looks up a port of a created node and checks its direction.
        /// </summary>
        /// <param name="nodeId">The source id of the node.</param>
        /// <param name="portName">The port name.</param>
        /// <param name="direction">The direction the wire end requires.</param>
        /// <returns>The port.</returns>
        private Port FindPort(string? nodeId, string? portName, PortDirection direction)
        {
            if (nodeId is null || !_nodes.TryGetValue(nodeId, out var node))
            {
                throw new ImportException($"A wire refers to an unknown node '{nodeId}'.");
            }

            // Source and target names are direction-specific, so an input and an output may share a name
            var column = direction == PortDirection.Output ? node.Outputs : node.Inputs;
            var wanted = direction == PortDirection.Output ? "output" : "input";
            return column.FirstOrDefault(p => string.Equals(p.Name, portName, StringComparison.Ordinal))
                ?? throw new ImportException($"Node '{node.Name}' (type '{node.Type.Key}') has no {wanted} port named '{portName}'.");
        }

        /// <summary>
        ///     Determines the attach order of the created elements.
        /// </summary>
        /// <remarks>
        ///     The stored z-order must name every element exactly once; without one, groups sit at
        ///     the back followed by nodes, annotations and wires.
        /// </remarks>
        /// <param name="groups">The created groups.</param>
        /// <param name="nodes">The created nodes.</param>
        /// <param name="annotations">The created annotations.</param>
        /// <param name="wires">The created wires.</param>
        private void OrderElements(List<Group> groups, List<Node> nodes, List<Annotation> annotations, List<Wire> wires)
        {
            if (_content.ZOrder is { } order)
            {
                if (order.Count != _all.Count)
                {
                    throw new ImportException("The z-order does not list every element exactly once.");
                }

                var placed = new HashSet<DiagramElement>();
                foreach (var id in order)
                {
                    if (id is null || !_all.TryGetValue(id, out var element) || !placed.Add(element))
                    {
                        throw new ImportException($"The z-order refers to an unknown or repeated element '{id}'.");
                    }

                    _result.AddElement(element);
                }

                return;
            }

            foreach (var element in groups.Cast<DiagramElement>().Concat(nodes).Concat(annotations).Concat(wires))
            {
                _result.AddElement(element);
            }
        }
    }
}
