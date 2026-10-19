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
        Assert.Equal((12, 14), (logic.Nodes.Count, logic.Wires.Count));
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
        var path = Path.Join(temp.Create(), "showcase.json");

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
        var path = Path.Join(temp.Create(), "audio.json");
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
        Assert.ThrowsAny<IOException>(() => DemoDocument.Load(Path.Join(temp.Path, "missing.json")));
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

    /// <summary>
    ///     Validates that the Inputs property of AND, OR and XOR gives the gate inputs a, b, c, ... clamped to 2..8.
    /// </summary>
    [Fact]
    public void DemoNodeTypes_Gates_InputsProperty_ProducesNamedInputs()
    {
        // Arrange
        var page = new Diagram(DemoNodeTypes.CreateRegistry()).Pages.Add("Main");
        var and = page.AddNode(DemoNodeTypes.And, 0, 0);
        var or = page.AddNode(DemoNodeTypes.Or, 0, 150);
        var xor = page.AddNode(DemoNodeTypes.Xor, 0, 300);
        var defaults = and.Inputs.Select(p => p.Name).ToArray();

        // Act
        and.Properties[DemoNodeTypes.GateInputsProperty] = 4;
        or.Properties[DemoNodeTypes.GateInputsProperty] = 1;
        xor.Properties[DemoNodeTypes.GateInputsProperty] = 9;

        // Assert: names follow the alphabet; out-of-range counts are clamped; the NOT gate has no such property
        Assert.Equal(["a", "b"], defaults);
        Assert.Equal(["a", "b", "c", "d"], and.Inputs.Select(p => p.Name));
        Assert.Equal(["a", "b"], or.Inputs.Select(p => p.Name));
        Assert.Equal(["a", "b", "c", "d", "e", "f", "g", "h"], xor.Inputs.Select(p => p.Name));
        Assert.All(and.Inputs, p => Assert.Equal($"Gate input {p.Name}.", p.Description));
        Assert.Null(page.Diagram.Types.Get(DemoNodeTypes.Not).FindProperty(DemoNodeTypes.GateInputsProperty));
        Assert.Equal(NodeGeometry.RequiredHeight(8, page.Diagram.Theme), xor.Bounds.Height);
    }

    /// <summary>
    ///     Validates the three-input AND gate of the logic sample.
    /// </summary>
    [Fact]
    public void DemoSamples_Logic_ThreeInputGate_IsWired()
    {
        // Arrange / Act
        var diagram = DemoSamples.Logic();
        var gate = diagram.FindNode("All")!;

        // Assert
        Assert.Equal(3, gate.Properties.Get<int>(DemoNodeTypes.GateInputsProperty));
        Assert.Equal(["a", "b", "c"], gate.Inputs.Select(p => p.Name));
        Assert.Equal(["SwitchA.out", "SwitchB.out", "SwitchC.out"], gate.Inputs.Select(p => p.Wires.Single().Source.ToString()));
        Assert.Equal("AllLamp.in", gate.Output("out").Wires.Single().Target.ToString());
    }

    /// <summary>
    ///     Validates that a file saved before gates had an Inputs property still loads with its wires on a and b.
    /// </summary>
    [Fact]
    public void DemoDocument_Load_OldTwoInputGateFile_KeepsWires()
    {
        // Arrange: a two-input gate wired on both inputs, saved without the Inputs property (as older versions wrote it)
        var diagram = new Diagram(DemoNodeTypes.CreateRegistry());
        var page = diagram.Pages.Add("Main");
        page.AddNode(DemoNodeTypes.Switch, 0, 0, "A");
        page.AddNode(DemoNodeTypes.Switch, 0, 150, "B");
        page.AddNode(DemoNodeTypes.Or, 240, 75, "G");
        page.Connect("A.out", "G.a");
        page.Connect("B.out", "G.b");
        var root = System.Text.Json.Nodes.JsonNode.Parse(diagram.ToJson())!;
        foreach (var node in root["pages"]![0]!["nodes"]!.AsArray())
        {
            (node!["properties"] as System.Text.Json.Nodes.JsonObject)?.Remove(DemoNodeTypes.GateInputsProperty);
        }

        using var temp = new TempDirectory();
        var path = Path.Join(temp.Create(), "old.json");
        File.WriteAllText(path, root.ToJsonString());

        // Act
        var loaded = DemoDocument.Load(path);

        // Assert
        var gate = loaded.FindNode("G")!;
        Assert.Equal(2, gate.Properties.Get<int>(DemoNodeTypes.GateInputsProperty));
        Assert.Equal(["A.out -> G.a", "B.out -> G.b"], loaded.Pages[0].Wires.Select(w => $"{w.Source} -> {w.Target}"));
    }

    /// <summary>
    ///     Validates that the oscillator has amplitude, phase and DC offset properties with their categories, units,
    ///     descriptions and defaults, outside the node summary so the node keeps its look.
    /// </summary>
    [Fact]
    public void DemoNodeTypes_Oscillator_HasAmplitudePhaseAndOffsetProperties()
    {
        // Arrange
        var type = DemoNodeTypes.CreateRegistry().Get(DemoNodeTypes.Oscillator);

        // Act
        var amplitude = type.FindProperty(DemoNodeTypes.AmplitudeProperty)!;
        var phase = type.FindProperty(DemoNodeTypes.PhaseProperty)!;
        var offset = type.FindProperty(DemoNodeTypes.DcOffsetProperty)!;

        // Assert
        Assert.Equal(1.0, amplitude.DefaultValue);
        Assert.False(string.IsNullOrEmpty(amplitude.Description));
        Assert.Equal(0.0, phase.DefaultValue);
        Assert.Equal("°", phase.Unit);
        Assert.False(string.IsNullOrEmpty(phase.Description));
        Assert.Equal(0.0, offset.DefaultValue);
        Assert.Equal("DC Offset", offset.DisplayName);
        Assert.False(string.IsNullOrEmpty(offset.Description));
        Assert.All([amplitude, phase, offset], p => Assert.False(p.ShowInSummary));

        // Assert: every setting is under "Oscillator", ordered waveform, frequency, amplitude, phase, DC offset
        Assert.All(type.Properties, p => Assert.Equal(DemoNodeTypes.OscillatorCategory, p.Category));
        Assert.Equal(
            [DemoNodeTypes.WaveformProperty, DemoNodeTypes.FrequencyProperty, DemoNodeTypes.AmplitudeProperty, DemoNodeTypes.PhaseProperty, DemoNodeTypes.DcOffsetProperty],
            type.Properties.OrderBy(p => p.Order).Select(p => p.Name));
    }

    /// <summary>
    ///     Validates that the envelope lists its properties under "Envelope" in attack, decay, sustain, release order, each
    ///     with a description.
    /// </summary>
    [Fact]
    public void DemoNodeTypes_Envelope_PropertiesInAdsrOrderUnderEnvelope()
    {
        // Arrange
        var type = DemoNodeTypes.CreateRegistry().Get(DemoNodeTypes.Envelope);

        // Act
        var ordered = type.Properties.OrderBy(p => p.Order).ToList();

        // Assert
        Assert.Equal(
            [DemoNodeTypes.AttackProperty, DemoNodeTypes.DecayProperty, DemoNodeTypes.SustainProperty, DemoNodeTypes.ReleaseProperty],
            ordered.Select(p => p.Name));
        Assert.Equal([1, 2, 3, 4], ordered.Select(p => p.Order));
        Assert.All(ordered, p => Assert.Equal(DemoNodeTypes.EnvelopeCategory, p.Category));
        Assert.All(ordered, p => Assert.False(string.IsNullOrEmpty(p.Description)));
    }

    /// <summary>
    ///     Validates that every demo node type has a description and a tooltip that starts with it.
    /// </summary>
    [Fact]
    public void DemoNodeTypes_EveryType_HasDescriptionAndToolTip()
    {
        // Arrange: one node of every type on a page, so live tooltips can read their page
        var diagram = new Diagram(DemoNodeTypes.CreateRegistry());
        var page = diagram.Pages.Add("Main");
        var nodes = diagram.Types.Select((type, i) => page.AddNode(type, new Point(0, i * 200.0))).ToList();

        // Act / Assert
        Assert.All(nodes, node =>
        {
            Assert.False(string.IsNullOrEmpty(node.Type.Description), node.Type.Key);
            var tip = node.Type.GetToolTip(node);
            Assert.NotNull(tip);
            Assert.StartsWith(node.Type.Description, tip, StringComparison.Ordinal);
        });
    }

    /// <summary>
    ///     Validates that every port of every demo node type has a description.
    /// </summary>
    [Fact]
    public void DemoNodeTypes_EveryPort_HasDescription()
    {
        // Arrange: nodes, so ports a provider computes are included too
        var diagram = new Diagram(DemoNodeTypes.CreateRegistry());
        var page = diagram.Pages.Add("Main");
        var nodes = diagram.Types.Select((type, i) => page.AddNode(type, new Point(0, i * 200.0))).ToList();

        // Act
        var ports = nodes.SelectMany(n => n.Ports).ToList();

        // Assert
        Assert.NotEmpty(ports);
        Assert.All(ports, port => Assert.False(string.IsNullOrEmpty(port.Description), $"{port.Node.Type.Key}.{port.Name}"));
    }

    /// <summary>
    ///     Validates that the audio sample's tooltips summarize the live settings of its filter and envelope.
    /// </summary>
    [Fact]
    public void DemoNodeTypes_AudioToolTips_SummarizeSettings()
    {
        // Arrange
        var diagram = DemoSamples.Audio();

        // Act
        var tone = diagram.FindNode("Tone")!;
        var shape = diagram.FindNode("Shape")!;

        // Assert
        Assert.Contains("Low-pass, 900 Hz", tone.Type.GetToolTip(tone), StringComparison.Ordinal);
        Assert.Contains("A 10 ms · D 120 ms · S 70 % · R 300 ms", shape.Type.GetToolTip(shape), StringComparison.Ordinal);
    }

    /// <summary>
    ///     Validates that the logic sample's note on the three-input AND gate explains the Inputs property in words that
    ///     hold for any value, and fits above the gate.
    /// </summary>
    [Fact]
    public void DemoSamples_Logic_GateAnnotation_DescribesInputsProperty()
    {
        // Arrange
        var page = DemoSamples.Logic().Pages[0];

        // Act
        var note = page.Annotations.Single(a => a.Text.Contains("Inputs property", StringComparison.Ordinal));
        var gate = page.Nodes.Single(n => n.Name == "All");

        // Assert
        Assert.Contains($"{DemoNodeTypes.MinGateInputs}–{DemoNodeTypes.MaxGateInputs}", note.Text, StringComparison.Ordinal);
        Assert.Contains("every input is on; an unconnected input is off", note.Text.Replace('\n', ' '), StringComparison.Ordinal);
        Assert.DoesNotContain("Inputs = 3", note.Text, StringComparison.Ordinal);
        Assert.True(note.Bounds.Bottom < gate.Bounds.Top);
    }

    /// <summary>
    ///     Validates that an oscillator saved before it had amplitude, phase and DC offset loads with their defaults and
    ///     keeps its saved waveform and frequency.
    /// </summary>
    [Fact]
    public void DemoDocument_Load_OldOscillatorFile_AppliesNewPropertyDefaults()
    {
        // Arrange: an oscillator saved without the new properties (as older versions wrote it)
        var diagram = new Diagram(DemoNodeTypes.CreateRegistry());
        var page = diagram.Pages.Add("Main");
        var osc = page.AddNode(DemoNodeTypes.Oscillator, 0, 0, "Osc");
        osc.Properties[DemoNodeTypes.WaveformProperty] = "Saw";
        osc.Properties[DemoNodeTypes.FrequencyProperty] = 220.0;
        var root = System.Text.Json.Nodes.JsonNode.Parse(diagram.ToJson())!;
        foreach (var properties in root["pages"]![0]!["nodes"]!.AsArray().Select(node => node!["properties"]).OfType<System.Text.Json.Nodes.JsonObject>())
        {
            properties.Remove(DemoNodeTypes.AmplitudeProperty);
            properties.Remove(DemoNodeTypes.PhaseProperty);
            properties.Remove(DemoNodeTypes.DcOffsetProperty);
        }

        using var temp = new TempDirectory();
        var path = Path.Join(temp.Create(), "old.json");
        File.WriteAllText(path, root.ToJsonString());

        // Act
        var loaded = DemoDocument.Load(path).FindNode("Osc")!;

        // Assert
        Assert.Equal("Saw", loaded.Properties[DemoNodeTypes.WaveformProperty]);
        Assert.Equal(220.0, loaded.Properties[DemoNodeTypes.FrequencyProperty]);
        Assert.Equal(1.0, loaded.Properties[DemoNodeTypes.AmplitudeProperty]);
        Assert.Equal(0.0, loaded.Properties[DemoNodeTypes.PhaseProperty]);
        Assert.Equal(0.0, loaded.Properties[DemoNodeTypes.DcOffsetProperty]);
        Assert.Equal(new OscillatorSettings("Saw", 220.0, 1.0, 0.0, 0.0), OscillatorSettings.FromNode(loaded));
    }

    /// <summary>
    ///     Validates the live tooltips of the logic node types and the descriptions of their ports.
    /// </summary>
    [Fact]
    public void DemoNodeTypes_LogicToolTips_ShowLiveState()
    {
        // Arrange
        var diagram = DemoSamples.Logic();
        var switchA = diagram.FindNode("SwitchA")!;
        var sum = diagram.FindNode("Sum")!;
        var lamp = diagram.FindNode("SumLamp")!;
        var carryLamp = diagram.FindNode("CarryLamp")!;

        // Act
        var switchTip = switchA.Type.GetToolTip(switchA);
        var gateTip = sum.Type.GetToolTip(sum);
        var lampTip = lamp.Type.GetToolTip(lamp);
        var darkTip = carryLamp.Type.GetToolTip(carryLamp);

        // Assert
        Assert.EndsWith("Now: on", switchTip, StringComparison.Ordinal);
        Assert.StartsWith("XOR gate.", gateTip, StringComparison.Ordinal);
        Assert.EndsWith("Output now: on", gateTip, StringComparison.Ordinal);
        Assert.EndsWith("Now: lit", lampTip, StringComparison.Ordinal);
        Assert.EndsWith("Now: dark", darkTip, StringComparison.Ordinal);
        Assert.Equal("Lights the lamp while on.", lamp.Input("in").Description);
    }
}
