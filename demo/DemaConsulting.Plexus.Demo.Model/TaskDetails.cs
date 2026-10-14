using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace DemaConsulting.Plexus.Demo.Model;

/// <summary>
///     Host data carried by <c>Task</c> nodes: the node data escape hatch, annotated so that the property grid shows and
///     edits it without any extra code.
/// </summary>
/// <remarks>
///     <para>
///         Hosts attach arbitrary payloads to a node through <see cref="Node.Data"/>. Registering the payload's exact type as
///         <see cref="NodeType.DataType"/> makes snapshots and clipboard interchange carry it (as JSON) without any type-name
///         resolution, so the class is a plain mutable class that <c>System.Text.Json</c> can read and write.
///     </para>
///     <para>
///         The properties demonstrate the annotations the property grid understands: <c>Category</c>, <c>Description</c>,
///         <c>DisplayName</c>, <c>ReadOnly</c>, <c>Browsable(false)</c> (<see cref="Revision"/>), <c>DefaultValue</c> (a value that
///         differs from it is bold), an enumeration (<see cref="Priority"/>), a flags enumeration (<see cref="Labels"/>), type
///         converters with standard values (<see cref="Team"/> is a closed list, <see cref="Reviewer"/> an editable one), an
///         expandable nested object (<see cref="Contact"/>), a collection edited in the collection editor
///         (<see cref="Checklist"/>, whose items hold a nested collection), <c>PasswordPropertyText</c>
///         (<see cref="AccessCode"/>) and a custom editor named by <c>UIHint</c> (<see cref="Progress"/>, a slider
///         that the demo registers).
///     </para>
/// </remarks>
public sealed class TaskDetails
{
    /// <summary>
    ///     Gets or sets the free-form notes describing what the task involves.
    /// </summary>
    [Category("Details"), DisplayName("Summary"), Description("What the task involves, in a sentence or two.")]
    public string Notes { get; set; } = string.Empty;

    /// <summary>
    ///     Gets or sets how urgent the task is.
    /// </summary>
    [Category("Details"), Description("How urgent the task is."), DefaultValue(TaskPriority.Normal)]
    public TaskPriority Priority { get; set; } = TaskPriority.Normal;

    /// <summary>
    ///     Gets or sets the labels of the task.
    /// </summary>
    [Category("Details"), Description("Labels that apply to the task; tick any combination."), DefaultValue(TaskLabels.None)]
    public TaskLabels Labels { get; set; }

    /// <summary>
    ///     Gets or sets how much of the work is done, in percent.
    /// </summary>
    [Category("Details"), Description("How much of the work is done, in percent."), DefaultValue(0), UIHint("slider")]
    public int Progress { get; set; }

    /// <summary>
    ///     Gets or sets the team that owns the task.
    /// </summary>
    [Category("People"), Description("The team that owns the task."), TypeConverter(typeof(TeamConverter)), DefaultValue("Operations")]
    public string Team { get; set; } = "Operations";

    /// <summary>
    ///     Gets or sets who signs the work off.
    /// </summary>
    [Category("People"), Description("Who signs the work off; pick a name or type another."), TypeConverter(typeof(ReviewerConverter))]
    public string Reviewer { get; set; } = string.Empty;

    /// <summary>
    ///     Gets or sets who to contact about the task.
    /// </summary>
    [Category("People"), Description("Who to contact about the task."), TypeConverter(typeof(ExpandableObjectConverter))]
    public ContactInfo Contact { get; set; } = new();

    /// <summary>
    ///     Gets or sets the steps of the task.
    /// </summary>
    [Category("Checklist"), Description("The steps of the task; each step can carry remarks.")]
    public List<ChecklistItem> Checklist { get; set; } = [];

    /// <summary>
    ///     Gets or sets a secret needed to open the task's attachments.
    /// </summary>
    [Category("Tracking"), Description("A secret needed to open the task's attachments."), PasswordPropertyText(true)]
    public string AccessCode { get; set; } = string.Empty;

    /// <summary>
    ///     Gets or sets the reference that was assigned when the task was created.
    /// </summary>
    [Category("Tracking"), Description("The reference that was assigned when the task was created."), ReadOnly(true)]
    public string Reference { get; set; } = "TSK-0001";

    /// <summary>
    ///     Gets or sets the revision counter, which is saved with the task but never shown.
    /// </summary>
    [Browsable(false)]
    public int Revision { get; set; }
}
