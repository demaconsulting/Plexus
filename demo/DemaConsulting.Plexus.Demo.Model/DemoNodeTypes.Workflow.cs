namespace DemaConsulting.Plexus.Demo.Model;

/// <content>
///     The business workflow node types: steps joined by control-flow wires.
/// </content>
public static partial class DemoNodeTypes
{
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
        types.Add(Define(Start, "START", "Start", WorkflowCategory, "Start",
            [],
            [new("next", Flow)],
            [Choice("Trigger", "Manual", ["Manual", "Scheduled", "Email"])]));

        // The task also carries host data (see TaskDetails) beyond its declared properties
        types.Add(Define(Task, "TASK", "Task", WorkflowCategory, "Task",
            [new("in", Flow) { AllowsMultipleWires = true }],
            [new("out", Flow)],
            [new PropertyDefinition("Assignee", "Unassigned") { ShowInSummary = true, Description = "Who is responsible for the task." }, Summarized("Duration", 2, "h", description: "The planned effort.")],
            dataType: typeof(TaskDetails)));
        types.Add(Define(Decision, "DECISION", "Decision", WorkflowCategory, "Decision",
            [new("in", Flow) { AllowsMultipleWires = true }],
            [new("yes", Flow), new("no", Flow)],
            [new PropertyDefinition("Condition", "Amount > 1000") { ShowInSummary = true }]));
        types.Add(Define(Approval, "APPROVAL", "Approval", WorkflowCategory, "Approval",
            [new("in", Flow) { AllowsMultipleWires = true }],
            [new("approved", Flow), new("rejected", Flow)],
            [new PropertyDefinition("Approver", "Manager") { ShowInSummary = true }, Summarized("Timeout", 48, "h")]));
        types.Add(Define(Notify, "NOTIFY", "Notify", WorkflowCategory, "Notify",
            [new("in", Flow) { AllowsMultipleWires = true }],
            [new("out", Flow)],
            [Choice("Channel", "Email", ["Email", "SMS", "Chat"]), new PropertyDefinition("Recipient", "Team") { ShowInSummary = true }]));
        types.Add(Define(End, "END", "End", WorkflowCategory, "End",
            [new("in", Flow) { AllowsMultipleWires = true }],
            [],
            [Choice("Outcome", "Completed", ["Completed", "Cancelled"])]));
    }
}
