using Avalonia.Media;
using DemaConsulting.Plexus.Avalonia.PropertyGrid.Plexus;
using PlexusColor = DemaConsulting.Plexus.Geometry.Color;
using PlexusRect = DemaConsulting.Plexus.Geometry.Rect;

namespace DemaConsulting.Plexus.Avalonia.PropertyGrid.Tests;

/// <summary>
///     Tests of <see cref="NodePropertySource"/>: the rows it lists and how edits reach the node.
/// </summary>
public class NodePropertySourceTests
{
    /// <summary>
    ///     Gets the item with a path.
    /// </summary>
    /// <param name="source">The source.</param>
    /// <param name="path">The name of the item.</param>
    /// <returns>The item.</returns>
    private static PropertyItem Item(IPropertySource source, string path) => source.GetItems().Single(i => i.Name == path);

    /// <summary>
    ///     The name row and the uncategorized properties are listed under General (first), the others under their category.
    /// </summary>
    [Fact]
    public void NodePropertySource_GetItems_ListsNameAndDeclaredPropertiesWithMetadata()
    {
        // Arrange
        var (_, node) = NodeFixture.Create();
        using var source = new NodePropertySource(node);

        // Act
        var groups = PropertyOrdering.Group(source.GetItems(), PropertySortMode.Categorized);

        // Assert: General comes first, with the name row first inside it
        Assert.Equal(["General", "Look", "Signal", "Tone"], groups.Select(g => g.Category));
        Assert.Equal(["Name", "Enabled", "Gain", "Label", "Serial"], groups[0].Items.Select(i => i.Name));
        var name = Item(source, "Name");
        Assert.Equal("(Name)", name.Label);
        Assert.Equal("Osc", name.GetValue());

        var frequency = Item(source, "Frequency");
        Assert.Equal("Frequency (Hz)", frequency.DisplayName);
        Assert.Equal("The pitch.", frequency.Description);
        Assert.Equal(typeof(double), frequency.PropertyType);
        Assert.False(frequency.IsChanged);
        Assert.Equal("440", frequency.GetText());

        Assert.True(Item(source, "Serial").IsReadOnly);
        Assert.Equal("slider", Item(source, "Gain").EditorKey);
        Assert.Equal(typeof(bool), Item(source, "Enabled").PropertyType);
    }

    /// <summary>
    ///     A choice property is an exclusive list of its choices and a color is shown as an Avalonia color.
    /// </summary>
    [Fact]
    public void NodePropertySource_GetItems_ChoiceAndColorRowsUseTheMatchingEditorTypes()
    {
        // Arrange
        var (_, node) = NodeFixture.Create();
        using var source = new NodePropertySource(node);

        // Act
        var waveform = Item(source, "Waveform");
        var tint = Item(source, "Tint");

        // Assert
        Assert.True(waveform.StandardValuesExclusive);
        Assert.Equal(["Sine", "Square", "Saw"], waveform.GetStandardValues().Select(v => v.Text));
        Assert.Equal(typeof(Color), tint.PropertyType);
        Assert.Equal(Color.FromArgb(255, 255, 165, 0), tint.GetValue());
        Assert.False(tint.IsChanged);
    }

    /// <summary>
    ///     The properties of the host data are listed with their annotations and hidden ones are left out.
    /// </summary>
    [Fact]
    public void NodePropertySource_GetItems_ListsHostDataProperties()
    {
        // Arrange
        var (_, node) = NodeFixture.Create();
        using var source = new NodePropertySource(node);

        // Act
        var level = Item(source, "Level");

        // Assert
        Assert.Equal("Tone", level.Category);
        Assert.Equal(5, level.GetValue());
        Assert.True(level.IsChanged);
        Assert.Equal(["a", "b"], Item(source, "Tags").GetCollectionValues());
        Assert.DoesNotContain(source.GetItems(), i => i.Name == "Hidden");
    }

    /// <summary>
    ///     A node without host data lists only the name and the declared properties.
    /// </summary>
    [Fact]
    public void NodePropertySource_GetItems_NodeWithoutDataHasNoDataRows()
    {
        // Arrange
        var (diagram, _) = NodeFixture.Create();
        var plain = diagram.Pages[0].AddNode(NodeFixture.PlainKey, 0, 200, name: "Plain");

        // Act
        using var source = new NodePropertySource(plain);

        // Assert
        Assert.Equal(["Name", "Mode"], source.GetItems().Select(i => i.Name));
    }

    /// <summary>
    ///     A node that is not attached to a diagram cannot be shown.
    /// </summary>
    [Fact]
    public void NodePropertySource_Constructor_DetachedNode_Throws()
    {
        // Arrange
        var (_, node) = NodeFixture.Create();
        node.Remove();

        // Act / Assert
        Assert.Throws<ArgumentException>(() => new NodePropertySource(node));
        Assert.Throws<ArgumentNullException>(() => new NodePropertySource(null!));
    }

    /// <summary>
    ///     Typed text is validated with the property definition: units are accepted and a mistake gives the reason.
    /// </summary>
    [Fact]
    public void NodePropertySource_SetText_ValidatesWithThePropertyDefinition()
    {
        // Arrange
        var (_, node) = NodeFixture.Create();
        using var source = new NodePropertySource(node);
        var session = new PropertyEditSession(source, PropertyEditMode.Staged);

        // Act
        var bad = session.SetText(Item(source, "Frequency"), "abc");
        var good = session.SetText(Item(source, "Frequency"), "880 Hz");
        var gain = session.SetText(Item(source, "Gain"), "x");

        // Assert
        Assert.False(bad.Succeeded);
        Assert.Equal("'abc' is not a number.", bad.Error);
        Assert.True(good.Succeeded);
        Assert.Equal(880.0, session.GetValue(Item(source, "Frequency")));
        Assert.False(gain.Succeeded);
        Assert.Equal(440.0, node.Properties["Frequency"]);
    }

    /// <summary>
    ///     An empty or duplicate name is refused while it is typed, with the reason of the rename pipeline.
    /// </summary>
    [Fact]
    public void NodePropertySource_SetText_NameIsValidatedWhileTyping()
    {
        // Arrange
        var (_, node) = NodeFixture.Create();
        using var source = new NodePropertySource(node);
        var session = new PropertyEditSession(source, PropertyEditMode.Staged);
        var name = Item(source, "Name");

        // Act
        var empty = session.SetText(name, "   ");
        var duplicate = session.SetText(name, "Other");

        // Assert
        Assert.Equal("A node name cannot be empty.", empty.Error);
        Assert.Equal("The name 'Other' is already used by another node.", duplicate.Error);
        Assert.Equal("Osc", node.Name);
    }

    /// <summary>
    ///     A staged commit is one undo entry for the rename, the properties and the data together.
    /// </summary>
    [Fact]
    public void NodePropertySource_StagedCommit_IsOneUndoEntry()
    {
        // Arrange
        var (diagram, node) = NodeFixture.Create();
        var original = node.Data;
        var before = diagram.History.UndoCount;
        using var source = new NodePropertySource(node);
        var session = new PropertyEditSession(source, PropertyEditMode.Staged);

        // Act
        session.SetText(Item(source, "Name"), "Lead");
        session.SetText(Item(source, "Frequency"), "880");
        session.SetText(Item(source, "Waveform"), "square");
        session.SetValue(Item(source, "Tint"), Colors.Red);
        session.SetText(Item(source, "Level"), "9");
        var result = session.Commit();

        // Assert
        Assert.True(result.Succeeded);
        Assert.Equal("Lead", node.Name);
        Assert.Equal(880.0, node.Properties["Frequency"]);
        Assert.Equal("Square", node.Properties["Waveform"]);
        Assert.Equal(PlexusColor.Red, node.Properties["Tint"]);
        Assert.Equal(9, ((ToneSettings)node.Data!).Level);
        Assert.NotSame(original, node.Data);
        Assert.Equal(5, ((ToneSettings)original!).Level);
        Assert.Equal(before + 1, diagram.History.UndoCount);

        diagram.History.Undo();
        Assert.Equal("Osc", node.Name);
        Assert.Equal(440.0, node.Properties["Frequency"]);
        Assert.Equal("Sine", node.Properties["Waveform"]);
        Assert.Same(original, node.Data);
        Assert.Equal(before, diagram.History.UndoCount);
    }

    /// <summary>
    ///     Cancelling a staged session changes nothing and the live data is never touched while editing.
    /// </summary>
    [Fact]
    public void NodePropertySource_StagedCancel_ChangesNothing()
    {
        // Arrange
        var (diagram, node) = NodeFixture.Create();
        var original = (ToneSettings)node.Data!;
        var before = diagram.History.UndoCount;
        using var source = new NodePropertySource(node);
        var session = new PropertyEditSession(source, PropertyEditMode.Staged);

        // Act
        session.SetText(Item(source, "Name"), "Lead");
        session.SetText(Item(source, "Level"), "9");
        session.Cancel();

        // Assert
        Assert.Equal("Osc", node.Name);
        Assert.Equal(5, original.Level);
        Assert.Same(original, node.Data);
        Assert.Equal(before, diagram.History.UndoCount);
        Assert.Equal(5, Item(source, "Level").GetValue());
    }

    /// <summary>
    ///     A commit that changes only the name leaves the data object alone.
    /// </summary>
    [Fact]
    public void NodePropertySource_StagedCommit_UneditedDataIsNotReplaced()
    {
        // Arrange
        var (diagram, node) = NodeFixture.Create();
        var original = node.Data;
        var before = diagram.History.UndoCount;
        using var source = new NodePropertySource(node);
        var session = new PropertyEditSession(source, PropertyEditMode.Staged);

        // Act
        session.SetText(Item(source, "Name"), "Lead");
        session.Commit();

        // Assert
        Assert.Same(original, node.Data);
        Assert.Equal("Lead", node.Name);
        Assert.Equal(before + 1, diagram.History.UndoCount);
    }

    /// <summary>
    ///     A rename that a handler vetoes refuses the whole commit, shows the reason on the name row and changes nothing.
    /// </summary>
    [Fact]
    public void NodePropertySource_StagedCommit_RenameVetoedRefusesTheCommitAndMarksTheNameRow()
    {
        // Arrange
        var (diagram, node) = NodeFixture.Create();
        diagram.NodeRenaming += (_, e) =>
        {
            e.Cancel = true;
            e.Reason = "Names starting with L are reserved.";
        };
        var before = diagram.History.UndoCount;
        using var source = new NodePropertySource(node);
        var session = new PropertyEditSession(source, PropertyEditMode.Staged);
        session.SetText(Item(source, "Name"), "Lead");
        session.SetText(Item(source, "Frequency"), "880");

        // Act
        var result = session.Commit();

        // Assert
        Assert.False(result.Succeeded);
        Assert.Equal("Names starting with L are reserved.", result.Error);
        Assert.Equal("Names starting with L are reserved.", session.GetError(Item(source, "Name")));
        Assert.Equal("Osc", node.Name);
        Assert.Equal(440.0, node.Properties["Frequency"]);
        Assert.Equal(before, diagram.History.UndoCount);
        Assert.True(session.HasChanges);
    }

    /// <summary>
    ///     In live mode each edit is its own undo entry, and a data edit publishes a copy while the old data stays untouched.
    /// </summary>
    [Fact]
    public void NodePropertySource_LiveEdits_EachEditIsItsOwnUndoEntry()
    {
        // Arrange
        var (diagram, node) = NodeFixture.Create();
        var original = (ToneSettings)node.Data!;
        var before = diagram.History.UndoCount;
        using var source = new NodePropertySource(node);
        var session = new PropertyEditSession(source, PropertyEditMode.Live);

        // Act
        session.SetText(Item(source, "Name"), "Lead");
        session.SetText(Item(source, "Frequency"), "880");
        session.SetText(Item(source, "Level"), "9");
        session.SetText(Item(source, "Level"), "7");

        // Assert
        Assert.Equal(before + 4, diagram.History.UndoCount);
        Assert.Equal("Lead", node.Name);
        Assert.Equal(880.0, node.Properties["Frequency"]);
        Assert.Equal(7, ((ToneSettings)node.Data!).Level);
        Assert.Equal(5, original.Level);

        diagram.History.Undo();
        Assert.Equal(9, ((ToneSettings)node.Data).Level);
        Assert.Equal(9, Item(source, "Level").GetValue());
    }

    /// <summary>
    ///     A collection of the host data is edited on the working copy and persists on commit.
    /// </summary>
    [Fact]
    public void NodePropertySource_StagedCommit_CollectionEditsPersistAndUndo()
    {
        // Arrange
        var (diagram, node) = NodeFixture.Create();
        using var source = new NodePropertySource(node);
        var session = new PropertyEditSession(source, PropertyEditMode.Staged);
        var tags = Item(source, "Tags");

        // Act
        var editor = session.BeginCollectionEdit(tags);
        editor.Add("c");
        editor.RemoveAt(0);
        var result = session.Commit();

        // Assert
        Assert.True(result.Succeeded);
        Assert.Equal(["b", "c"], ((ToneSettings)node.Data!).Tags);
        diagram.History.Undo();
        Assert.Equal(["a", "b"], ((ToneSettings)node.Data).Tags);
    }

    /// <summary>
    ///     A change made from elsewhere (undo) raises Changed and the rows show the new values.
    /// </summary>
    [Fact]
    public void NodePropertySource_ExternalChange_RaisesChangedAndRereads()
    {
        // Arrange
        var (diagram, node) = NodeFixture.Create();
        using var source = new NodePropertySource(node);
        var raised = 0;
        source.Changed += (_, _) => raised++;
        node.Properties["Frequency"] = 100.0;
        var afterFirst = raised;

        // Act
        diagram.History.Undo();

        // Assert
        Assert.True(afterFirst > 0);
        Assert.True(raised > afterFirst);
        Assert.Equal(440.0, Item(source, "Frequency").GetValue());
    }

    /// <summary>
    ///     A changed data type (undo to a different payload) rebuilds the data rows.
    /// </summary>
    [Fact]
    public void NodePropertySource_ExternalChange_DataRemovedDropsTheDataRows()
    {
        // Arrange
        var (_, node) = NodeFixture.Create();
        using var source = new NodePropertySource(node);

        // Act
        node.Data = null;

        // Assert
        Assert.DoesNotContain(source.GetItems(), i => i.Name == "Level");
    }

    /// <summary>
    ///     The factory chooses the Plexus sources for nodes, annotations and groups, and the grid uses them.
    /// </summary>
    [Fact]
    public void PropertySourceFactory_Create_PlexusElements_UsesThePlexusSources()
    {
        // Arrange
        var (diagram, node) = NodeFixture.Create();
        var annotation = diagram.Pages[0].AddAnnotation("note", new PlexusRect(0, 100, 120, 40));
        var group = diagram.Pages[0].CreateGroup([node], "Frame");

        // Act / Assert
        Assert.IsType<NodePropertySource>(PropertySourceFactory.Create(node));
        Assert.IsType<AnnotationPropertySource>(PropertySourceFactory.Create(annotation));
        Assert.IsType<GroupPropertySource>(PropertySourceFactory.Create(group));
    }

    /// <summary>
    ///     An annotation's text and fill are edited as one undo entry and a cleared fill goes back to the theme.
    /// </summary>
    [Fact]
    public void AnnotationPropertySource_StagedCommit_IsOneUndoEntry()
    {
        // Arrange
        var (diagram, _) = NodeFixture.Create();
        var annotation = diagram.Pages[0].AddAnnotation("note", new PlexusRect(0, 100, 120, 40));
        var before = diagram.History.UndoCount;
        using var source = new AnnotationPropertySource(annotation);
        var session = new PropertyEditSession(source, PropertyEditMode.Staged);

        // Act
        session.SetText(Item(source, "Text"), "changed");
        session.SetValue(Item(source, "Fill"), Colors.Gold);
        var result = session.Commit();

        // Assert
        Assert.True(result.Succeeded);
        Assert.Equal("changed", annotation.Text);
        Assert.Equal(PlexusColor.FromArgb(255, 255, 215, 0), annotation.Fill);
        Assert.Equal(before + 1, diagram.History.UndoCount);

        var cleared = new PropertyEditSession(source, PropertyEditMode.Live);
        cleared.SetValue(Item(source, "Fill"), null);
        Assert.Null(annotation.Fill);
    }

    /// <summary>
    ///     A group's title and collapsed state are edited as one undo entry.
    /// </summary>
    [Fact]
    public void GroupPropertySource_StagedCommit_IsOneUndoEntry()
    {
        // Arrange
        var (diagram, node) = NodeFixture.Create();
        var group = diagram.Pages[0].CreateGroup([node], "Frame");
        var before = diagram.History.UndoCount;
        using var source = new GroupPropertySource(group);
        var session = new PropertyEditSession(source, PropertyEditMode.Staged);

        // Act
        session.SetText(Item(source, "Title"), "Renamed");
        session.SetValue(Item(source, "IsCollapsed"), true);
        var result = session.Commit();

        // Assert
        Assert.True(result.Succeeded);
        Assert.Equal("Renamed", group.Title);
        Assert.True(group.IsCollapsed);
        Assert.Equal(before + 1, diagram.History.UndoCount);
        diagram.History.Undo();
        Assert.Equal("Frame", group.Title);
        Assert.False(group.IsCollapsed);
    }
}
