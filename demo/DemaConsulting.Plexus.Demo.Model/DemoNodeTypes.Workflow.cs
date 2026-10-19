namespace DemaConsulting.Plexus.Demo.Model;

/// <content>
///     The business workflow node types: steps joined by control-flow wires.
/// </content>
public static partial class DemoNodeTypes
{
    /// <summary>
    ///     The description of an input that the preceding steps feed.
    /// </summary>
    private const string StepsLeadHere = "The steps that lead here.";

    /// <summary>
    ///     The key of the workflow start node type.
    /// </summary>
    public const string Start = "demo.workflow.Start";

    /// <summary>
    ///     The key of the task node type, which carries a <see cref="TaskDetails"/> payload.
    /// </summary>
    public const string Task = "demo.workflow.Task";

    /// <summary>
    ///     The key of the decision node type.
    /// </summary>
    public const string Decision = "demo.workflow.Decision";

    /// <summary>
    ///     The key of the approval node type.
    /// </summary>
    public const string Approval = "demo.workflow.Approval";

    /// <summary>
    ///     The key of the notification node type.
    /// </summary>
    public const string Notify = "demo.workflow.Notify";

    /// <summary>
    ///     The key of the workflow end node type.
    /// </summary>
    public const string End = "demo.workflow.End";

    /// <summary>
    ///     Adds the workflow node types to a registry.
    /// </summary>
    /// <remarks>
    ///     Every step input opts into fan-in (<see cref="PortDefinition.AllowsMultipleWires"/>): paths of a
    ///     workflow merge naturally, so several steps may lead into the same next step.
    /// </remarks>
    /// <param name="types">The registry to fill.</param>
    private static void AddWorkflowTypes(NodeTypeRegistry types)
    {
        const string start = "Where the workflow begins.";
        types.Add(Define(Start, "START", "Start", WorkflowCategory, "Start",
            [],
            [new("next", Flow) { Description = "The first step." }],
            [Choice("Trigger", "Manual", ["Manual", "Scheduled", "Email"], description: "What starts the workflow.")],
            description: start,
            toolTip: Live(start, node => $"Trigger: {Value(node, "Trigger")}")));

        // The task also carries host data (see TaskDetails) beyond its declared properties
        const string task = "A piece of work someone does.";
        types.Add(Define(Task, "TASK", "Task", WorkflowCategory, "Task",
            [new("in", Flow) { AllowsMultipleWires = true, Description = StepsLeadHere }],
            [new("out", Flow) { Description = "The next step, once the task is done." }],
            [new PropertyDefinition("Assignee", "Unassigned") { ShowInSummary = true, Description = "Who is responsible for the task." }, Summarized("Duration", 2, "h", description: "The planned effort.")],
            dataType: typeof(TaskDetails),
            createData: () => new TaskDetails(),
            description: task,
            toolTip: Live(task, node => $"{Value(node, "Assignee")} · {Value(node, "Duration")}")));
        const string decision = "Chooses the next path by a condition.";
        types.Add(Define(Decision, "DECISION", "Decision", WorkflowCategory, "Decision",
            [new("in", Flow) { AllowsMultipleWires = true, Description = StepsLeadHere }],
            [new("yes", Flow) { Description = "Taken when the condition holds." }, new("no", Flow) { Description = "Taken otherwise." }],
            [new PropertyDefinition("Condition", "Amount > 1000") { ShowInSummary = true, Description = "The rule that picks the path." }],
            description: decision,
            toolTip: Live(decision, node => $"If {Value(node, "Condition")}")));
        const string approval = "Waits for someone to approve or reject.";
        types.Add(Define(Approval, "APPROVAL", "Approval", WorkflowCategory, "Approval",
            [new("in", Flow) { AllowsMultipleWires = true, Description = StepsLeadHere }],
            [new("approved", Flow) { Description = "Taken when approved." }, new("rejected", Flow) { Description = "Taken when rejected." }],
            [
                new PropertyDefinition("Approver", "Manager") { ShowInSummary = true, Description = "Who approves." },
                Summarized("Timeout", 48, "h", description: "How long to wait for an answer."),
            ],
            description: approval,
            toolTip: Live(approval, node => $"{Value(node, "Approver")} · {Value(node, "Timeout")} timeout")));
        const string notify = "Sends a message and continues.";
        types.Add(Define(Notify, "NOTIFY", "Notify", WorkflowCategory, "Notify",
            [new("in", Flow) { AllowsMultipleWires = true, Description = StepsLeadHere }],
            [new("out", Flow) { Description = "The next step." }],
            [
                Choice("Channel", "Email", ["Email", "SMS", "Chat"], description: "How the message is sent."),
                new PropertyDefinition("Recipient", "Team") { ShowInSummary = true, Description = "Who receives the message." },
            ],
            description: notify,
            toolTip: Live(notify, node => $"{Value(node, "Channel")} to {Value(node, "Recipient")}")));
        const string end = "Where the workflow finishes.";
        types.Add(Define(End, "END", "End", WorkflowCategory, "End",
            [new("in", Flow) { AllowsMultipleWires = true, Description = StepsLeadHere }],
            [],
            [Choice("Outcome", "Completed", ["Completed", "Cancelled"], description: "How the workflow ends.")],
            description: end,
            toolTip: Live(end, node => $"Outcome: {Value(node, "Outcome")}")));
    }
}
