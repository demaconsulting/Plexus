using System.Text.Json;
using DemaConsulting.Plexus.Demo.Model;
using DemaConsulting.Plexus.Geometry;
using DemaConsulting.Plexus.Rendering;
using DemaConsulting.Plexus.Scene;

namespace DemaConsulting.Plexus.Demo.Tests;

/// <summary>
///     Tests for the demo model: node types, samples and the snapshot file format.
/// </summary>
public class DemoModelTests
{
    /// <summary>
    ///     Validates that the registry holds both port types and every node type with an icon.
    /// </summary>
    [Fact]
    public void DemoNodeTypes_CreateRegistry_RegistersPortTypesAndNodeTypes()
    {
        // Arrange / Act
        var registry = DemoNodeTypes.CreateRegistry();

        // Assert
        Assert.Equal(Color.Parse("#FF8C00"), registry.PortTypes.Get(DemoNodeTypes.Number).Color);
        Assert.Equal(Color.Parse("#1E90FF"), registry.PortTypes.Get(DemoNodeTypes.Fluid).Color);
        Assert.Equal(
            [DemoNodeTypes.Number, DemoNodeTypes.Bool, DemoNodeTypes.Fluid, DemoNodeTypes.Audio, DemoNodeTypes.Flow],
            registry.PortTypes.Select(p => p.Key));
        Assert.Equal(30, registry.Count);
        Assert.All(registry, type => Assert.NotNull(type.Icon));
        Assert.Equal(
            [DemoNodeTypes.MathCategory, DemoNodeTypes.PlumbingCategory, DemoNodeTypes.LogicCategory, DemoNodeTypes.AudioCategory, DemoNodeTypes.WorkflowCategory],
            registry.Select(t => t.Category!).Distinct());
        Assert.NotSame(registry, DemoNodeTypes.CreateRegistry());
    }

    /// <summary>
    ///     Validates that FlowMeter declares flow on both sides and that the choice property needs no custom summary.
    /// </summary>
    [Fact]
    public void DemoNodeTypes_FlowMeter_HasSameNamedInputAndOutputPorts()
    {
        // Arrange
        var diagram = DemoSamples.Create("plumbing");
        var meter = diagram.FindNode("Meter")!;
        var drainType = diagram.Types.Get(DemoNodeTypes.Drain);
        var meterType = diagram.Types.Get(DemoNodeTypes.FlowMeter);

        // Assert
        Assert.Equal(["flow", "reading"], meter.Outputs.Select(p => p.Name));
        Assert.Equal(PortDirection.Input, meter.Input("flow").Direction);
        Assert.Equal(PortDirection.Output, meter.Output("flow").Direction);
        Assert.Throws<InvalidOperationException>(() => meter.Port("flow"));
        Assert.Null(drainType.Summary);
        Assert.Equal(PropertyValueKind.Choice, meterType.FindProperty("Unit")!.ValueKind);
    }

    /// <summary>
    ///     Validates that node summaries list the summarized properties with their units.
    /// </summary>
    [Fact]
    public void DemoNodeTypes_Summary_ListsSummarizedPropertiesWithUnits()
    {
        // Arrange
        var page = DemoSamples.Plumbing().Pages[0];
        var feed = page.Nodes.Single(n => n.Name == "Feed");
        var supply = page.Nodes.Single(n => n.Name == "Supply");

        // Act / Assert
        Assert.Equal(["Fluid: Water", "Rate: 12 L/min", "Head: 20 m"], feed.Type.GetSummaryLines(feed));
        Assert.Equal(["Fluid: Water", "Capacity: 500 L", "Level: 80 %"], supply.Type.GetSummaryLines(supply));
    }

    /// <summary>
    ///     Validates that every sample builds with a clean history and the expected structure.
    /// </summary>
    [Fact]
    public void DemoSamples_Create_AllSamples_BuildCleanDiagrams()
    {
        // Arrange / Act
        var diagrams = DemoSamples.Names.ToDictionary(name => name, DemoSamples.Create);

        // Assert
        Assert.Equal(["math", "plumbing", "logic", "audio", "workflow", "groups", "wiring", "showcase"], DemoSamples.Names);
        Assert.All(diagrams.Values, diagram => Assert.False(diagram.History.CanUndo));
        var math = diagrams["math"].Pages.Single();
        Assert.Equal((7, 6, 1), (math.Nodes.Count, math.Wires.Count, math.Annotations.Count));
        var plumbing = diagrams["plumbing"].Pages.Single();
        Assert.Equal((14, 11), (plumbing.Nodes.Count, plumbing.Wires.Count));
        var logic = diagrams["logic"].Pages.Single();
        Assert.Equal((9, 10), (logic.Nodes.Count, logic.Wires.Count));
        var audio = diagrams["audio"].Pages.Single();
        Assert.Equal((7, 6), (audio.Nodes.Count, audio.Wires.Count));
        var workflow = diagrams["workflow"].Pages.Single();
        Assert.Equal((8, 8), (workflow.Nodes.Count, workflow.Wires.Count));
        Assert.Contains("expense request", workflow.Annotations.Single().Text, StringComparison.Ordinal);
        Assert.Equal(
            ["Math", "Plumbing", "Logic", "Audio", "Workflow", "Groups", "Wiring"],
            diagrams["showcase"].Pages.Select(p => p.Name).ToArray());
    }

    /// <summary>
    ///     Validates that every wire of every sample satisfies the demo's connection rules, so no sample relies on fan-in into an input that does not allow it.
    /// </summary>
    [Fact]
    public void DemoSamples_Create_AllSamples_EveryWireSatisfiesTheConnectionRules()
    {
        // Arrange
        var diagrams = DemoSamples.Names.Select(DemoSamples.Create).ToList();

        // Act: judge each wire as if it were being moved to its own ports (the wire does not count against itself)
        var violations = diagrams
            .SelectMany(d => d.Pages)
            .SelectMany(p => p.Wires.Select(w => (Wire: w, Check: p.CanReconnect(w, null, null))))
            .Where(r => !r.Check.IsAllowed)
            .Select(r => $"{r.Wire.Source} -> {r.Wire.Target}: {r.Check.Reason}")
            .ToList();

        // Assert
        Assert.Empty(violations);
    }

    /// <summary>
    ///     Validates that the workflow step inputs and the scope input opt into fan-in while other demo inputs keep the single-wire default.
    /// </summary>
    [Fact]
    public void DemoNodeTypes_FanIn_OnlyWorkflowStepsAndScopeOptIn()
    {
        // Arrange
        var registry = DemoNodeTypes.CreateRegistry();
        var optedIn = registry
            .SelectMany(t => t.Inputs.Where(i => i.AllowsMultipleWires).Select(i => $"{t.Key}.{i.Name}"))
            .Order()
            .ToList();

        // Assert
        Assert.Equal(
            [
                "demo.math.Scope.signal",
                "demo.workflow.Approval.in",
                "demo.workflow.Decision.in",
                "demo.workflow.End.in",
                "demo.workflow.Notify.in",
                "demo.workflow.Task.in",
            ],
            optedIn);
    }

    /// <summary>
    ///     Validates that the demo keeps the single-wire default for inputs that do not opt in.
    /// </summary>
    [Fact]
    public void DemoSamples_Math_SecondWireIntoPlainInput_IsRefusedWithReason()
    {
        // Arrange: an Add node whose first input is wired already
        var page = DemoSamples.Math().Pages[0];
        var add = page.Nodes.First(n => n.Type.Key == DemoNodeTypes.Add && n.Input("a").Wires.Count == 1);
        var extra = page.AddNode(DemoNodeTypes.Constant, 900, 600);

        // Act
        var plain = page.CanConnect(extra.Output("value"), add.Input("a"));

        // Assert
        Assert.False(plain.IsAllowed);
        Assert.Contains("does not accept more than one", plain.Reason!, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Validates that the wiring page shows junction dots with square T corners, a crossing hop, a merge dot where two nets feed an input that allows fan-in, and no dot where different nets merely touch.
    /// </summary>
    [Fact]
    public void DemoSamples_Wiring_Page_ShowsDotsHopsAndMergeDotForOptedInFanIn()
    {
        // Arrange
        var page = DemoSamples.Wiring().Pages.Single();

        // Act
        var scene = SceneBuilder.Build(page, new SceneOptions { ShowGrid = false });
        var dotOwners = scene.Primitives.OfType<RectPrimitive>()
            .Where(r => r.Element is Wire && r.Stroke is null && r.CornerRadius > 0)
            .Select(r => ((Wire)r.Element!).Source.Node.Name)
            .ToList();
        var across = page.Wires.Single(w => w.Source.Node.Name == "HopAcross");
        var hopping = scene.Primitives.OfType<PathPrimitive>().Single(p => ReferenceEquals(p.Element, across) && p.Fill is null);
        var merged = page.Wires.Where(w => w.Target.Node.Name == "InScope").Select(w => w.Source.Node.Name).ToList();

        // Assert: fan-outs, the split, the dashed net, the collapsed group and the opted-in merge have dots; touching nets do not
        Assert.Multiple(
            () => Assert.Contains("FanA", dotOwners),
            () => Assert.Contains("FanC", dotOwners),
            () => Assert.Contains("SplitSw", dotOwners),
            () => Assert.Contains("SteamTank", dotOwners),
            () => Assert.Contains("GroupSum", dotOwners),
            () => Assert.Equal(["InA", "InB"], merged.Order().ToArray()),
            () => Assert.Single(dotOwners, name => merged.Contains(name)),
            () => Assert.DoesNotContain(dotOwners, name => name.StartsWith("Touch", StringComparison.Ordinal) || name.StartsWith("Hop", StringComparison.Ordinal)),
            () => Assert.Equal(2, hopping.Commands.Count(c => c.Kind == PathCommandKind.CubicTo)));
    }

    /// <summary>
    ///     Validates that the groups page demonstrates a nested group and a collapsed group.
    /// </summary>
    [Fact]
    public void DemoSamples_Showcase_GroupsPage_HasNestedAndCollapsedGroups()
    {
        // Arrange / Act
        var groups = DemoSamples.Showcase().Pages[5].Groups;

        // Assert
        Assert.Equal(3, groups.Count);
        Assert.Contains(groups, g => g.Members.OfType<Group>().Any());
        Assert.Contains(groups, g => g.IsCollapsed);
    }

    /// <summary>
    ///     Validates that unknown sample names are rejected and known names are case-insensitive.
    /// </summary>
    [Fact]
    public void DemoSamples_Create_Name_IsCaseInsensitiveAndValidated()
    {
        // Arrange / Act / Assert
        Assert.Single(DemoSamples.Create("PlumBing").Pages);
        Assert.Throws<ArgumentException>(() => DemoSamples.Create("nope"));
        Assert.Throws<ArgumentNullException>(() => DemoSamples.Create(null!));
    }

    /// <summary>
    ///     Validates that saving and loading a sample preserves its structure, including host data on Task nodes.
    /// </summary>
    [Fact]
    public void DemoDocument_SaveAndLoad_RoundTripsStructureAndHostData()
    {
        // Arrange
        using var temp = new TempDirectory();
        var original = DemoSamples.Showcase();
        var path = Path.Combine(temp.Create(), "showcase.json");

        // Act
        DemoDocument.Save(original, path);
        var loaded = DemoDocument.Load(path);

        // Assert: same pages, nodes, wires, groups; the TaskDetails payload survives
        Assert.Equal(
            original.Pages.Select(p => (p.Name, p.Nodes.Count, p.Wires.Count, p.Groups.Count)),
            loaded.Pages.Select(p => (p.Name, p.Nodes.Count, p.Wires.Count, p.Groups.Count)));
        Assert.Equal(JsonSerializer.Serialize(original.FindNode("Review")!.Data), JsonSerializer.Serialize(loaded.FindNode("Review")?.Data));
        Assert.Equal("Check the request for completeness", ((TaskDetails)loaded.FindNode("Review")!.Data!).Notes);
        Assert.False(loaded.History.CanUndo);
    }

    /// <summary>
    ///     Validates that the annotated host data of a Task survives every route: a deep copy, the snapshot JSON and a duplicate
    ///     (the same codec the clipboard uses), including nested objects, both collection levels, enumerations and the hidden revision.
    /// </summary>
    [Fact]
    public void DemoModel_TaskDetails_RoundTripsThroughCloneSnapshotAndDuplicate()
    {
        // Arrange
        var diagram = DemoSamples.Showcase();
        var review = diagram.FindNode("Review")!;
        var original = (TaskDetails)review.Data!;
        original.Revision = 7;

        // Act
        var clone = (TaskDetails)review.CloneData()!;
        var loaded = Diagram.FromJson(diagram.ToJson(), DemoNodeTypes.CreateRegistry());
        var fromJson = (TaskDetails)loaded.FindNode("Review")!.Data!;
        var duplicate = (TaskDetails)((Node)review.Page!.Duplicate([review], 40, 40)[0]).Data!;

        // Assert
        var expected = JsonSerializer.Serialize(original);
        foreach (var copy in new[] { clone, fromJson, duplicate })
        {
            Assert.NotSame(original, copy);
            Assert.Equal(expected, JsonSerializer.Serialize(copy));
            Assert.Equal(TaskPriority.High, copy.Priority);
            Assert.Equal(TaskLabels.Feature | TaskLabels.NeedsReview, copy.Labels);
            Assert.Equal("Sam Rivera", copy.Contact.Name);
            Assert.Equal(3, copy.Checklist.Count);
            Assert.Equal(["Use the current quarter", "Ask for the receipt if it is missing"], copy.Checklist[1].Remarks);
            Assert.True(copy.Checklist[0].Done);
            Assert.Equal(7, copy.Revision);
            Assert.Equal("TSK-0042", copy.Reference);
        }

        Assert.NotSame(original.Checklist, clone.Checklist);
        Assert.NotSame(original.Contact, clone.Contact);
    }
    /// <summary>
    ///     Validates that a saved and reloaded sample renders to exactly the same pixels as the original.
    /// </summary>
    [Fact]
    public void DemoDocument_Load_SavedSample_RendersIdentically()
    {
        // Arrange
        using var temp = new TempDirectory();
        var original = DemoSamples.Audio();
        var path = Path.Combine(temp.Create(), "audio.json");
        DemoDocument.Save(original, path);

        // Act
        var reloaded = DemoDocument.Load(path);

        // Assert
        Assert.Equal(PlexusRenderer.RenderPng(original.Pages[0]), PlexusRenderer.RenderPng(reloaded.Pages[0]));
    }

    /// <summary>
    ///     Validates the argument checks of the document helpers.
    /// </summary>
    [Fact]
    public void DemoDocument_Arguments_AreValidated()
    {
        // Arrange
        using var temp = new TempDirectory();

        // Act / Assert
        Assert.Throws<ArgumentException>(() => DemoDocument.Load(" "));
        Assert.Throws<ArgumentNullException>(() => DemoDocument.Save(null!, "x.json"));
        Assert.Throws<ArgumentException>(() => DemoDocument.Save(DemoSamples.Audio(), string.Empty));
        Assert.ThrowsAny<IOException>(() => DemoDocument.Load(Path.Combine(temp.Path, "missing.json")));
    }

    /// <summary>
    ///     Validates that every sample page renders to a PNG (the demo's end-to-end smoke test).
    /// </summary>
    [Fact]
    public void DemoSamples_Showcase_EveryPage_RendersPng()
    {
        // Arrange / Act
        var pngs = DemoSamples.Showcase().Pages.Select(page => PlexusRenderer.RenderPng(page)).ToList();

        // Assert
        Assert.Equal(7, pngs.Count);
        Assert.All(pngs, png => Assert.True(png.Length > 1000));
    }
}
