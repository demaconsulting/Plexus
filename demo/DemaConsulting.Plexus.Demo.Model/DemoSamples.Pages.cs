using DemaConsulting.Plexus.Geometry;

namespace DemaConsulting.Plexus.Demo.Model;

/// <content>
///     The page builders: one per demo domain, plus the groups page.
/// </content>
public static partial class DemoSamples
{
    /// <summary>
    ///     The horizontal distance between node columns, a whole number of grid cells (15 units).
    /// </summary>
    private const double ColumnPitch = 240;

    /// <summary>
    ///     Adds the math page: a sine wave is scaled, offset, clamped and shown on a scope.
    /// </summary>
    /// <param name="page">The page to fill.</param>
    private static void AddMathPage(DiagramPage page)
    {
        page.AddAnnotation("Dataflow: wave x gain + bias,\nlimited to Min..Max and shown on a scope.", new Rect(15, 15, 330, 48));
        AddNode(page, DemoNodeTypes.Sine, "Wave", 45, 90, ("Amplitude", 2.0), ("Frequency", 0.5));
        AddNode(page, DemoNodeTypes.Constant, "Gain", 45, 285, ("Value", 1.5));
        AddNode(page, DemoNodeTypes.Multiply, "Scale", 45 + ColumnPitch, 165);
        AddNode(page, DemoNodeTypes.Constant, "Bias", 45 + ColumnPitch, 345, ("Value", 0.25));
        AddNode(page, DemoNodeTypes.Add, "Offset", 45 + (2 * ColumnPitch), 225);
        AddNode(page, DemoNodeTypes.Clamp, "Limit", 45 + (3 * ColumnPitch), 225, ("Min", -1.0), ("Max", 1.0));
        AddNode(page, DemoNodeTypes.Scope, "Display", 45 + (4 * ColumnPitch), 225);

        // Wire after every node exists so the router can steer around them; "Node.port" paths name each end
        page.Connect("Wave.value", "Scale.a");
        page.Connect("Gain.value", "Scale.b");
        page.Connect("Scale.product", "Offset.a");
        page.Connect("Bias.value", "Offset.b");
        page.Connect("Offset.sum", "Limit.value");
        page.Connect("Limit.result", "Display.signal");
    }

    /// <summary>
    ///     Adds the plumbing page: a pumped supply passes a valve and a flow meter, then splits to a reservoir and a drain;
    ///     below it an oil circuit and a steam circuit show that wires take the color of the fluid they carry.
    /// </summary>
    /// <param name="page">The page to fill.</param>
    private static void AddPlumbingPage(DiagramPage page)
    {
        page.AddAnnotation("Water is pumped through a valve and a flow meter, then split\nbetween a reservoir and a drain. The meter reports to a scope.", new Rect(15, 15, 450, 48));
        AddNode(page, DemoNodeTypes.Tank, "Supply", 45, 90, ("Level", 80));
        AddNode(page, DemoNodeTypes.Pump, "Feed", 45 + ColumnPitch, 90);
        AddNode(page, DemoNodeTypes.Valve, "Throttle", 45 + (2 * ColumnPitch), 90, ("Open", 60));
        AddNode(page, DemoNodeTypes.FlowMeter, "Meter", 45 + (3 * ColumnPitch), 90);
        AddNode(page, DemoNodeTypes.Junction, "Split", 45 + (4 * ColumnPitch), 90);
        AddNode(page, DemoNodeTypes.Tank, "Reservoir", 45 + (5 * ColumnPitch), 90, ("Capacity", 1000), ("Level", 10));
        AddNode(page, DemoNodeTypes.Drain, "Sewer", 45 + (5 * ColumnPitch), 300);
        AddNode(page, DemoNodeTypes.Scope, "FlowDisplay", 45 + (3 * ColumnPitch), 300);

        // The flow meter has "flow" on both sides: as a target the input is used, as a source the output
        page.Connect("Supply.out", "Feed.in");
        page.Connect("Feed.out", "Throttle.in");
        page.Connect("Throttle.out", "Meter.flow");
        page.Connect("Meter.flow", "Split.in");
        page.Connect("Split.a", "Reservoir.in");
        page.Connect("Split.b", "Sewer.in");
        page.Connect("Meter.reading", "FlowDisplay.signal");

        // Two more circuits, one oil and one steam: wires are colored by the fluid the pump or tank upstream supplies
        page.AddAnnotation("Pipe colors follow the fluid: water blue, oil brown, steam dashed gray.", new Rect(15, 420, 480, 30));
        AddNode(page, DemoNodeTypes.Pump, "OilPump", 45, 495, ("Fluid", "Oil"));
        AddNode(page, DemoNodeTypes.Valve, "OilValve", 45 + ColumnPitch, 495);
        AddNode(page, DemoNodeTypes.Tank, "OilTank", 45 + (2 * ColumnPitch), 495, ("Fluid", "Oil"), ("Level", 30));
        AddNode(page, DemoNodeTypes.Tank, "Boiler", 45 + (3 * ColumnPitch), 495, ("Fluid", "Steam"), ("Level", 65));
        AddNode(page, DemoNodeTypes.Valve, "SteamValve", 45 + (4 * ColumnPitch), 495);
        AddNode(page, DemoNodeTypes.Drain, "Condenser", 45 + (5 * ColumnPitch), 495);
        page.Connect("OilPump.out", "OilValve.in");
        page.Connect("OilValve.out", "OilTank.in");
        page.Connect("Boiler.out", "SteamValve.in");
        page.Connect("SteamValve.out", "Condenser.in");
    }

    /// <summary>
    ///     Adds the logic page: two switches drive a half adder, an OR gate and an inverter, each ending in a lamp;
    ///     below them a third switch joins the first two at a three-input AND gate (its <c>Inputs</c> property is 3), with a
    ///     note on how that property works.
    /// </summary>
    /// <param name="page">The page to fill.</param>
    private static void AddLogicPage(DiagramPage page)
    {
        page.AddAnnotation("Two switches drive XOR (sum), AND (carry) and OR gates;\nthe inverter reports when neither switch is on.", new Rect(15, 15, 420, 48));
        AddNode(page, DemoNodeTypes.Switch, "SwitchA", 45, 150, ("On", true));
        AddNode(page, DemoNodeTypes.Switch, "SwitchB", 45, 345);
        AddNode(page, DemoNodeTypes.Xor, "Sum", 45 + ColumnPitch, 90);
        AddNode(page, DemoNodeTypes.And, "Carry", 45 + ColumnPitch, 240);
        AddNode(page, DemoNodeTypes.Or, "Either", 45 + ColumnPitch, 390);
        AddNode(page, DemoNodeTypes.Not, "Neither", 45 + (2 * ColumnPitch), 397.5);
        AddNode(page, DemoNodeTypes.Lamp, "SumLamp", 45 + (3 * ColumnPitch), 75, ("Color", "Green"));
        AddNode(page, DemoNodeTypes.Lamp, "CarryLamp", 45 + (3 * ColumnPitch), 225, ("Color", "Red"));
        AddNode(page, DemoNodeTypes.Lamp, "NeitherLamp", 45 + (3 * ColumnPitch), 375);

        page.Connect("SwitchA.out", "Sum.a");
        page.Connect("SwitchB.out", "Sum.b");
        page.Connect("SwitchA.out", "Carry.a");
        page.Connect("SwitchB.out", "Carry.b");
        page.Connect("SwitchA.out", "Either.a");
        page.Connect("SwitchB.out", "Either.b");
        page.Connect("Sum.out", "SumLamp.in");
        page.Connect("Carry.out", "CarryLamp.in");
        page.Connect("Either.out", "Neither.in");
        page.Connect("Neither.out", "NeitherLamp.in");

        // A gate whose ports vary per node: its Inputs property sets the number of inputs (3 gives this AND gate a third
        // input "c" and makes it taller). The note stays true for any value: the evaluator reads an unconnected input as off
        page.AddAnnotation(
            $"This AND gate's Inputs property ({DemoNodeTypes.MinGateInputs}–{DemoNodeTypes.MaxGateInputs}) sets how\nmany inputs it has. It lights the lamp only when\nevery input is on; an unconnected input is off.",
            new Rect(390, 480, 360, 64));
        AddNode(page, DemoNodeTypes.Switch, "SwitchC", 45, 555);
        AddNode(page, DemoNodeTypes.And, "All", 45 + ColumnPitch, 562.5, (DemoNodeTypes.GateInputsProperty, 3));
        AddNode(page, DemoNodeTypes.Lamp, "AllLamp", 45 + (3 * ColumnPitch), 555);
        page.Connect("SwitchA.out", "All.a");
        page.Connect("SwitchB.out", "All.b");
        page.Connect("SwitchC.out", "All.c");
        page.Connect("All.out", "AllLamp.in");
    }

    /// <summary>
    ///     Adds the audio page: two oscillators are mixed, filtered, shaped by an envelope and sent to a speaker.
    /// </summary>
    /// <param name="page">The page to fill.</param>
    private static void AddAudioPage(DiagramPage page)
    {
        page.AddAnnotation("A two-oscillator voice: mix, filter, then an envelope\nshapes the amplifier gain on its way to the speaker.", new Rect(15, 15, 420, 48));
        AddNode(page, DemoNodeTypes.Oscillator, "Osc1", 45, 90, ("Waveform", "Saw"), ("Frequency", 220.0));
        AddNode(page, DemoNodeTypes.Oscillator, "Osc2", 45, 285, ("Waveform", "Square"), ("Frequency", 221.5));
        AddNode(page, DemoNodeTypes.Mixer, "Mix", 45 + ColumnPitch, 165);
        AddNode(page, DemoNodeTypes.Filter, "Tone", 45 + (2 * ColumnPitch), 165, ("Cutoff", 900.0));
        AddNode(page, DemoNodeTypes.Envelope, "Shape", 45 + (2 * ColumnPitch), 345);
        // The amplifier has two inputs, so its first row sits half a grid cell above its center; shifting it (and the speaker after it) down by half a cell keeps the signal wires straight
        AddNode(page, DemoNodeTypes.Amplifier, "Amp", 45 + (3 * ColumnPitch), 172.5);
        AddNode(page, DemoNodeTypes.Speaker, "Out", 45 + (4 * ColumnPitch), 172.5);

        page.Connect("Osc1.out", "Mix.a");
        page.Connect("Osc2.out", "Mix.b");
        page.Connect("Mix.mix", "Tone.in");
        page.Connect("Tone.out", "Amp.in");
        page.Connect("Shape.level", "Amp.gain");
        page.Connect("Amp.out", "Out.in");
    }

    /// <summary>
    ///     Adds the workflow page: a request is reviewed, checked, approved and confirmed, or declined.
    /// </summary>
    /// <param name="page">The page to fill.</param>
    private static void AddWorkflowPage(DiagramPage page)
    {
        page.AddAnnotation("An expense request: review it, check the amount, get sign-off,\nthen confirm. Anything invalid or rejected is declined.", new Rect(15, 15, 450, 48));
        AddNode(page, DemoNodeTypes.Start, "Request", 45, 90, ("Trigger", "Email"));
        var review = AddNode(page, DemoNodeTypes.Task, "Review", 45 + ColumnPitch, 90, ("Assignee", "Alex"), ("Duration", 4));
        review.Data = new TaskDetails
        {
            Notes = "Check the request for completeness",
            Priority = TaskPriority.High,
            Labels = TaskLabels.Feature | TaskLabels.NeedsReview,
            Progress = 25,
            Team = "Finance",
            Reviewer = "Dana",
            Contact = new ContactInfo { Name = "Sam Rivera", Email = "sam.rivera@example.com", Phone = "+1 555 0100" },
            AccessCode = "open-sesame",
            Reference = "TSK-0042",
            Checklist =
            [
                new ChecklistItem { Text = "Read the request", Done = true },
                new ChecklistItem { Text = "Compare the amount with the budget", Remarks = ["Use the current quarter", "Ask for the receipt if it is missing"] },
                new ChecklistItem { Text = "Write the summary" },
            ],
        };
        AddNode(page, DemoNodeTypes.Decision, "Check", 45 + (2 * ColumnPitch), 90);
        AddNode(page, DemoNodeTypes.Approval, "SignOff", 45 + (3 * ColumnPitch), 90);
        AddNode(page, DemoNodeTypes.Notify, "Confirm", 45 + (4 * ColumnPitch), 90, ("Recipient", "Requester"));
        AddNode(page, DemoNodeTypes.End, "Done", 45 + (5 * ColumnPitch), 90);
        AddNode(page, DemoNodeTypes.Notify, "Decline", 45 + (4 * ColumnPitch), 300, ("Channel", "Chat"), ("Recipient", "Requester"));
        AddNode(page, DemoNodeTypes.End, "Closed", 45 + (5 * ColumnPitch), 300, ("Outcome", "Cancelled"));

        page.Connect("Request.next", "Review.in");
        page.Connect("Review.out", "Check.in");
        page.Connect("Check.yes", "SignOff.in");
        page.Connect("SignOff.approved", "Confirm.in");
        page.Connect("Confirm.out", "Done.in");
        page.Connect("Check.no", "Decline.in");
        page.Connect("SignOff.rejected", "Decline.in");
        page.Connect("Decline.out", "Closed.in");
    }

    /// <summary>
    ///     Adds the groups page: an expanded group nesting another group, and a collapsed group
    ///     whose boundary-crossing wires attach to its box.
    /// </summary>
    /// <param name="page">The page to fill.</param>
    private static void AddGroupsPage(DiagramPage page)
    {
        page.AddAnnotation("Groups nest: Tone is inside Voice. The collapsed Pad group\nkeeps its wires attached to the box.", new Rect(780, 15, 420, 48));

        // An oscillator and filter form an inner group that is itself a member of the voice group
        var osc = AddNode(page, DemoNodeTypes.Oscillator, "VoiceOsc", 60, 135);
        var filter = AddNode(page, DemoNodeTypes.Filter, "VoiceFilter", 60 + ColumnPitch, 135);
        var amp = AddNode(page, DemoNodeTypes.Amplifier, "VoiceAmp", 60 + (2 * ColumnPitch), 142.5);
        var envelope = AddNode(page, DemoNodeTypes.Envelope, "VoiceEnv", 60 + ColumnPitch, 330);
        osc.Port("out").ConnectTo(filter.Input("in"));
        filter.Port("out").ConnectTo(amp.Input("in"));
        envelope.Port("level").ConnectTo(amp.Input("gain"));
        var tone = page.CreateGroup([osc, filter], "Tone");
        page.CreateGroup([tone, amp, envelope], "Voice");

        // A second voice is collapsed: wires out of it attach to the collapsed box
        var padOsc = AddNode(page, DemoNodeTypes.Oscillator, "PadOsc", 60, 615, ("Waveform", "Triangle"));
        var padFilter = AddNode(page, DemoNodeTypes.Filter, "PadFilter", 60 + ColumnPitch, 615, ("Type", "High-pass"));
        padOsc.Port("out").ConnectTo(padFilter.Input("in"));
        var pad = page.CreateGroup([padOsc, padFilter], "Pad");
        pad.IsCollapsed = true;

        // Both voices meet in a mixer outside the groups
        var mix = AddNode(page, DemoNodeTypes.Mixer, "VoiceMix", 60 + (3 * ColumnPitch), 345);
        var speaker = AddNode(page, DemoNodeTypes.Speaker, "VoiceOut", 60 + (4 * ColumnPitch), 345);
        amp.Port("out").ConnectTo(mix.Input("a"));
        padFilter.Port("out").ConnectTo(mix.Input("b"));
        mix.Port("mix").ConnectTo(speaker.Input("in"));
    }
}
