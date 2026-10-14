using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Threading;
using DemaConsulting.Plexus.Scene;
using PlexusPage = DemaConsulting.Plexus.DiagramPage;

namespace DemaConsulting.Plexus.Avalonia.Tests;

/// <summary>
///     A clipboard that keeps its content in memory, so tests do not depend on the machine's clipboard.
/// </summary>
public sealed class FakeClipboard : IDiagramClipboard
{
    /// <summary>
    ///     Gets or sets the text the clipboard holds.
    /// </summary>
    public string? Text { get; set; }

    /// <inheritdoc />
    public Task SetAsync(string json)
    {
        Text = json;
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task<string?> GetAsync() => Task.FromResult(Text);
}

/// <summary>
///     Hosts a <see cref="DiagramEditor"/> in a headless window with a small diagram model and helpers that drive it like a user.
/// </summary>
/// <remarks>
///     The model uses four test node types: <c>test.Source</c> (one f32 output), <c>test.Sink</c>
///     (one f32 input), <c>test.IntSink</c> (one i32 input) and <c>test.Merger</c> (one f32 input that
///     allows several wires). The editor fills the window, so
///     window coordinates equal editor coordinates, and the view starts at zoom 1 with no offset,
///     which makes diagram coordinates equal screen coordinates until a test changes the view.
/// </remarks>
public sealed class EditorHost : IDisposable
{
    /// <summary>
    ///     The last pointer position the helper moved to, where the next movement starts from.
    /// </summary>
    private Point _last;

    /// <summary>
    ///     Initializes a new instance of the <see cref="EditorHost"/> class with an empty page.
    /// </summary>
    /// <param name="width">The window width.</param>
    /// <param name="height">The window height.</param>
    public EditorHost(double width = 800, double height = 600)
    {
        var types = new NodeTypeRegistry();
        types.PortTypes.Add(new PortType("f32", Geometry.Color.Parse("#FF8C00")));
        types.PortTypes.Add(new PortType("i32", Geometry.Color.Parse("#1E90FF")));
        types.Add(new NodeType("test.Source") { Title = "SOURCE", Outputs = { new PortDefinition("value", "f32") } });
        types.Add(new NodeType("test.Sink") { Title = "SINK", Inputs = { new PortDefinition("value", "f32") } });
        types.Add(new NodeType("test.IntSink") { Title = "INT SINK", Inputs = { new PortDefinition("value", "i32") } });
        types.Add(new NodeType("test.Merger") { Title = "MERGER", Inputs = { new PortDefinition("value", "f32") { AllowsMultipleWires = true } } });
        Diagram = new Diagram(types);
        Page = Diagram.Pages.Add("Main");
        Editor = new DiagramEditor { Page = Page };
        Window = new Window { Width = width, Height = height, Content = Editor };
        Window.Show();
        Flush();
    }

    /// <summary>
    ///     Gets the diagram.
    /// </summary>
    public Diagram Diagram { get; }

    /// <summary>
    ///     Gets the page the editor shows.
    /// </summary>
    public PlexusPage Page { get; }

    /// <summary>
    ///     Gets the editor under test.
    /// </summary>
    public DiagramEditor Editor { get; }

    /// <summary>
    ///     Gets the window holding the editor.
    /// </summary>
    public Window Window { get; }

    /// <summary>
    ///     Gets the scene the editor currently shows.
    /// </summary>
    public DiagramScene CurrentScene => Editor.Scene ?? throw new InvalidOperationException("The editor has no scene.");

    /// <summary>
    ///     Gets the center of a node's box on screen.
    /// </summary>
    /// <param name="node">The node.</param>
    /// <returns>The screen point.</returns>
    public Point CenterOf(Node node) => Editor.DiagramToScreen(new Point(node.Bounds.Center.X, node.Bounds.Center.Y));

    /// <summary>
    ///     Gets the on-screen center of a node's name label (a double-click there renames).
    /// </summary>
    /// <param name="node">The node.</param>
    /// <returns>The screen point.</returns>
    public Point NameOf(Node node)
    {
        if (!CurrentScene.TryGetNodeLayout(node, out var layout))
        {
            throw new InvalidOperationException("The node is not in the scene.");
        }

        var center = layout.Name.Bounds.Center;
        return Editor.DiagramToScreen(new Point(center.X, center.Y));
    }

    /// <summary>
    ///     Lets pending layout, input and rendering work finish.
    /// </summary>
    public void Flush()
    {
        Window.UpdateLayout();
        Dispatcher.UIThread.RunJobs();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
    }

    /// <summary>
    ///     Clicks the left button at a point, optionally with modifiers held.
    /// </summary>
    /// <param name="at">The point relative to the editor.</param>
    /// <param name="modifiers">The modifiers to hold.</param>
    public void Click(Point at, RawInputModifiers modifiers = RawInputModifiers.None)
    {
        Window.MouseMove(at, modifiers);
        Window.MouseDown(at, MouseButton.Left, modifiers);
        Window.MouseUp(at, MouseButton.Left, modifiers);
        Flush();
    }

    /// <summary>
    ///     Double-clicks the left button at a point.
    /// </summary>
    /// <param name="at">The point relative to the editor.</param>
    public void DoubleClick(Point at)
    {
        Window.MouseMove(at);
        Window.MouseDown(at, MouseButton.Left);
        Window.MouseUp(at, MouseButton.Left);
        Window.MouseDown(at, MouseButton.Left);
        Window.MouseUp(at, MouseButton.Left);
        Flush();
    }

    /// <summary>
    ///     Drags with the left button from one point to another in several steps.
    /// </summary>
    /// <param name="from">Where the button goes down.</param>
    /// <param name="to">Where the button goes up.</param>
    /// <param name="modifiers">The modifiers to hold.</param>
    public void Drag(Point from, Point to, RawInputModifiers modifiers = RawInputModifiers.None)
    {
        BeginDrag(from, modifiers);
        MoveTo(to, modifiers);
        EndDrag(to, modifiers);
    }

    /// <summary>
    ///     Presses the left button and moves a little, leaving the drag in progress.
    /// </summary>
    /// <param name="from">Where the button goes down.</param>
    /// <param name="modifiers">The modifiers to hold.</param>
    public void BeginDrag(Point from, RawInputModifiers modifiers = RawInputModifiers.None)
    {
        _last = from;
        Window.MouseMove(from, modifiers);
        Window.MouseDown(from, MouseButton.Left, modifiers);
    }

    /// <summary>
    ///     Moves the pointer to a point in a few steps, as a hand would, while a button is held.
    /// </summary>
    /// <param name="to">The destination.</param>
    /// <param name="modifiers">The modifiers to hold.</param>
    public void MoveTo(Point to, RawInputModifiers modifiers = RawInputModifiers.None)
    {
        // Intermediate steps matter: a single jump would skip the drag threshold logic
        var start = _last;
        for (var i = 1; i <= 4; i++)
        {
            var f = i / 4.0;
            Window.MouseMove(new Point(start.X + ((to.X - start.X) * f), start.Y + ((to.Y - start.Y) * f)), modifiers | RawInputModifiers.LeftMouseButton);
        }

        _last = to;
        Flush();
    }

    /// <summary>
    ///     Releases the left button.
    /// </summary>
    /// <param name="at">Where the button goes up.</param>
    /// <param name="modifiers">The modifiers to hold.</param>
    public void EndDrag(Point at, RawInputModifiers modifiers = RawInputModifiers.None)
    {
        Window.MouseUp(at, MouseButton.Left, modifiers);
        Flush();
    }

    /// <summary>
    ///     Presses and releases a key, optionally with modifiers.
    /// </summary>
    /// <param name="key">The key.</param>
    /// <param name="modifiers">The modifiers to hold.</param>
    public void Press(Key key, RawInputModifiers modifiers = RawInputModifiers.None)
    {
        Editor.Focus();
        Window.KeyPress(key, modifiers, PhysicalKey.None, null);
        Window.KeyRelease(key, modifiers, PhysicalKey.None, null);
        Flush();
    }

    /// <summary>
    ///     Presses and releases a key without moving keyboard focus first (for typing into the rename box).
    /// </summary>
    /// <param name="key">The key.</param>
    /// <param name="modifiers">The modifiers to hold.</param>
    public void PressFocused(Key key, RawInputModifiers modifiers = RawInputModifiers.None)
    {
        Window.KeyPress(key, modifiers, PhysicalKey.None, null);
        Window.KeyRelease(key, modifiers, PhysicalKey.None, null);
        Flush();
    }

    /// <summary>
    ///     Clicks the right mouse button at a point.
    /// </summary>
    /// <param name="at">The point relative to the editor.</param>
    public void RightClick(Point at)
    {
        Window.MouseMove(at);
        Window.MouseDown(at, MouseButton.Right);
        Window.MouseUp(at, MouseButton.Right);
        Flush();
    }

    /// <summary>
    ///     Adds a <c>test.Source</c> node.
    /// </summary>
    /// <param name="x">The left edge.</param>
    /// <param name="y">The top edge.</param>
    /// <param name="name">The name, or null for an automatic one.</param>
    /// <returns>The node.</returns>
    public Node AddSource(double x, double y, string? name = null) => Page.AddNode("test.Source", x, y, name);

    /// <summary>
    ///     Adds a <c>test.Sink</c> node.
    /// </summary>
    /// <param name="x">The left edge.</param>
    /// <param name="y">The top edge.</param>
    /// <param name="name">The name, or null for an automatic one.</param>
    /// <returns>The node.</returns>
    public Node AddSink(double x, double y, string? name = null) => Page.AddNode("test.Sink", x, y, name);

    /// <summary>
    ///     Gets the on-screen position of a node's only port.
    /// </summary>
    /// <param name="node">The node.</param>
    /// <returns>The port's connection point on screen.</returns>
    public Point PortOf(Node node) => Editor.GetPortScreenPosition(node.Port("value"));

    /// <summary>
    ///     Scans the window for a point where hit testing reports a kind of hit on an element.
    /// </summary>
    /// <param name="kind">The kind of hit to find.</param>
    /// <param name="element">The element it must belong to.</param>
    /// <returns>A screen point with that hit.</returns>
    /// <exception cref="InvalidOperationException">Thrown when no such point exists.</exception>
    public Point FindHit(HitKind kind, DiagramElement element)
    {
        for (var y = 0; y < Window.Height; y += 2)
        {
            for (var x = 0; x < Window.Width; x += 2)
            {
                var hit = Editor.HitTest(new Point(x, y));
                if (hit.Kind == kind && ReferenceEquals(hit.Element, element))
                {
                    return new Point(x, y);
                }
            }
        }

        throw new InvalidOperationException($"No {kind} hit found for the element.");
    }

    /// <inheritdoc />
    public void Dispose()
    {
        Window.Close();
        GC.SuppressFinalize(this);
    }
}
