using System.ComponentModel;

namespace DemaConsulting.Plexus.Demo.Model;

/// <summary>
///     How urgent a task is; shown as a drop-down list in the properties dialog.
/// </summary>
public enum TaskPriority
{
    /// <summary>The task can wait.</summary>
    Low,

    /// <summary>The usual urgency.</summary>
    Normal,

    /// <summary>The task should be done soon.</summary>
    High,

    /// <summary>The task must be done now.</summary>
    Urgent,
}

/// <summary>
///     Labels that can be combined on a task; shown as a check list in the properties dialog.
/// </summary>
[Flags]
public enum TaskLabels
{
    /// <summary>No label.</summary>
    None = 0,

    /// <summary>The task fixes a problem.</summary>
    Bug = 1,

    /// <summary>The task adds something new.</summary>
    Feature = 2,

    /// <summary>The task is about documentation.</summary>
    Documentation = 4,

    /// <summary>The task needs a second pair of eyes.</summary>
    NeedsReview = 8,
}

/// <summary>
///     A way to reach a person: an expandable object in the properties dialog.
/// </summary>
/// <remarks>
///     <see cref="object.ToString"/> is the text shown on the collapsed row.
/// </remarks>
public sealed class ContactInfo
{
    /// <summary>
    ///     Gets or sets the person's name.
    /// </summary>
    [Description("The person's full name.")]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    ///     Gets or sets the e-mail address.
    /// </summary>
    [DisplayName("E-mail"), Description("The address to write to.")]
    public string Email { get; set; } = string.Empty;

    /// <summary>
    ///     Gets or sets the phone number.
    /// </summary>
    [Description("The number to call.")]
    public string Phone { get; set; } = string.Empty;

    /// <inheritdoc />
    public override string ToString() => string.IsNullOrWhiteSpace(Name) ? "(nobody)" : Name;
}

/// <summary>
///     One step of a task's checklist, shown as an element of the collection editor (with a nested list of remarks).
/// </summary>
/// <remarks>
///     The expandable-object converter on the class makes the collection editor show the step's properties next to the list.
/// </remarks>
[TypeConverter(typeof(ExpandableObjectConverter))]
public sealed class ChecklistItem
{
    /// <summary>
    ///     Gets or sets what has to be done.
    /// </summary>
    [DisplayName("Step"), Description("What has to be done in this step.")]
    public string Text { get; set; } = string.Empty;

    /// <summary>
    ///     Gets or sets a value indicating whether the step is finished.
    /// </summary>
    [DefaultValue(false), Description("Whether the step is finished.")]
    public bool Done { get; set; }

    /// <summary>
    ///     Gets or sets remarks about the step: a collection nested in a collection element.
    /// </summary>
    [Description("Remarks about the step, one per entry.")]
    public List<string> Remarks { get; set; } = [];

    /// <inheritdoc />
    public override string ToString() => (Done ? "[x] " : "[ ] ") + (string.IsNullOrWhiteSpace(Text) ? "(empty step)" : Text);
}

/// <summary>
///     A type converter that offers the teams of the organization as a closed list (a drop-down).
/// </summary>
public sealed class TeamConverter : StringConverter
{
    /// <summary>
    ///     The teams.
    /// </summary>
    private static readonly string[] Teams = ["Operations", "Finance", "Engineering", "Support"];

    /// <inheritdoc />
    public override bool GetStandardValuesSupported(ITypeDescriptorContext? context) => true;

    /// <inheritdoc />
    public override bool GetStandardValuesExclusive(ITypeDescriptorContext? context) => true;

    /// <inheritdoc />
    public override StandardValuesCollection GetStandardValues(ITypeDescriptorContext? context) => new(Teams);
}

/// <summary>
///     A type converter that suggests reviewers but also accepts any other name (an editable drop-down).
/// </summary>
public sealed class ReviewerConverter : StringConverter
{
    /// <summary>
    ///     The suggested reviewers.
    /// </summary>
    private static readonly string[] Reviewers = ["Dana", "Jordan", "Morgan", "Riley"];

    /// <inheritdoc />
    public override bool GetStandardValuesSupported(ITypeDescriptorContext? context) => true;

    /// <inheritdoc />
    public override bool GetStandardValuesExclusive(ITypeDescriptorContext? context) => false;

    /// <inheritdoc />
    public override StandardValuesCollection GetStandardValues(ITypeDescriptorContext? context) => new(Reviewers);
}
