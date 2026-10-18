using System.Collections.ObjectModel;

namespace DemaConsulting.Plexus.Avalonia.PropertyGrid.Tests;

/// <summary>
///     Unit tests of the EditSession unit: collection editing through a working copy.
/// </summary>
public class EditSessionCollectionTests
{
    /// <summary>
    ///     The replacement array used when a whole value is set over an open collection edit.
    /// </summary>
    private static readonly int[] ReplacementScores = [7, 8];

    /// <summary>
    ///     A staged edit of a get-only list leaves the target alone, shows the working copy, and commit refills the same list.
    /// </summary>
    [Fact]
    public void CollectionEditor_Staged_ListEditsApplyInPlaceOnCommit()
    {
        using var source = TestHelpers.PersonSource(out var person);
        person.Notes.AddRange(["a", "b", "c"]);
        var original = person.Notes;
        var session = new PropertyEditSession(source, PropertyEditMode.Staged);
        var notes = TestHelpers.Item(source, "Notes");

        var editor = session.BeginCollectionEdit(notes);
        Assert.True(editor.Add("d").Succeeded);
        Assert.True(editor.RemoveAt(0).Succeeded);
        Assert.True(editor.Move(0, 2).Succeeded);
        Assert.True(editor.SetItem(1, "D").Succeeded);

        Assert.Equal(["a", "b", "c"], person.Notes);
        Assert.Equal(["c", "D", "b"], editor.Items.Cast<string>());
        Assert.Equal(3, editor.Count);
        Assert.Equal(typeof(string), editor.ItemType);
        Assert.Same(notes, editor.Item);
        Assert.True(session.HasChanges);
        Assert.Equal(PendingEditKind.Collection, Assert.Single(session.GetPendingEdits()).Kind);
        Assert.Same(editor.Items, session.GetValue(notes));
        Assert.Equal("(Collection)", session.GetText(notes));

        Assert.True(session.Commit().Succeeded);
        Assert.Same(original, person.Notes);
        Assert.Equal(["c", "D", "b"], person.Notes);
        Assert.False(session.HasChanges);
    }

    /// <summary>
    ///     While a collection edit is pending the same editor is returned, and it ends with the commit.
    /// </summary>
    [Fact]
    public void CollectionEditor_Staged_SameEditorWhilePendingAndInactiveAfterCommit()
    {
        using var source = TestHelpers.PersonSource(out _);
        var session = new PropertyEditSession(source, PropertyEditMode.Staged);
        var notes = TestHelpers.Item(source, "Notes");
        var editor = session.BeginCollectionEdit(notes);
        editor.Add("x");

        Assert.Same(editor, session.BeginCollectionEdit(notes));
        session.Commit();

        Assert.False(editor.IsActive);
        var result = editor.Add("y");
        Assert.False(result.Succeeded);
        Assert.Equal("This collection edit has ended; begin a new one.", result.Error);
        var next = session.BeginCollectionEdit(notes);
        Assert.NotSame(editor, next);
        Assert.Equal(["x"], next.Items.Cast<string>());
    }

    /// <summary>
    ///     Cancel ends active collection editors and drops their pending edit.
    /// </summary>
    [Fact]
    public void CollectionEditor_Staged_CancelEndsTheEditor()
    {
        using var source = TestHelpers.PersonSource(out var person);
        var session = new PropertyEditSession(source, PropertyEditMode.Staged);
        var editor = session.BeginCollectionEdit(TestHelpers.Item(source, "Notes"));
        editor.Add("x");

        session.Cancel();

        Assert.False(editor.IsActive);
        Assert.False(session.HasChanges);
        Assert.Empty(person.Notes);
        Assert.False(editor.RemoveAt(0).Succeeded);
    }

    /// <summary>
    ///     When the working copy equals the target again the pending edit disappears.
    /// </summary>
    [Fact]
    public void CollectionEditor_Staged_BackToTheTargetRemovesThePendingEdit()
    {
        using var source = TestHelpers.PersonSource(out var person);
        person.Notes.Add("a");
        var session = new PropertyEditSession(source, PropertyEditMode.Staged);
        var editor = session.BeginCollectionEdit(TestHelpers.Item(source, "Notes"));

        editor.Add("b");
        Assert.True(session.HasChanges);
        editor.RemoveAt(1);

        Assert.False(session.HasChanges);
    }

    /// <summary>
    ///     New elements are created with the default constructor, with empty strings and defaults for value types.
    /// </summary>
    [Fact]
    public void CollectionEditor_Add_CreatesDefaultElements()
    {
        using var source = TestHelpers.PersonSource(out var person);
        person.Scores = [1];
        var session = new PropertyEditSession(source, PropertyEditMode.Staged);

        var tags = session.BeginCollectionEdit(TestHelpers.Item(source, "Tags"));
        var notes = session.BeginCollectionEdit(TestHelpers.Item(source, "Notes"));
        var scores = session.BeginCollectionEdit(TestHelpers.Item(source, "Scores"));
        Assert.True(tags.Add().Succeeded);
        Assert.True(notes.Add().Succeeded);
        Assert.True(scores.Insert(0).Succeeded);

        Assert.IsType<Tag>(tags.Items[0]);
        Assert.Equal(string.Empty, notes.Items[0]);
        Assert.Equal([0, 1], scores.Items.Cast<int>());
        session.Commit();
        Assert.Single(person.Tags);
        Assert.Equal([0, 1], person.Scores);
    }

    /// <summary>
    ///     An element type without a default constructor needs the session's factory.
    /// </summary>
    [Fact]
    public void CollectionEditor_Add_UsesTheFactoryWhenThereIsNoDefaultConstructor()
    {
        using var source = TestHelpers.PersonSource(out var person);
        var session = new PropertyEditSession(source, PropertyEditMode.Staged);
        var editor = session.BeginCollectionEdit(TestHelpers.Item(source, "Badges"));

        var missing = editor.Add();
        session.CollectionItemFactory = item => new Badge(item.Name.Length);
        var created = editor.Add();
        session.CollectionItemFactory = _ => throw new InvalidOperationException("factory broke");
        var broken = editor.Add();

        Assert.False(missing.Succeeded);
        Assert.Contains("CollectionItemFactory", missing.Error);
        Assert.True(created.Succeeded);
        Assert.Equal("factory broke", broken.Error);
        session.Commit();
        Assert.Equal(6, Assert.Single(person.Badges).Level);
    }

    /// <summary>
    ///     An array of unchanged length is reordered in place even behind a get-only property; changing its length there fails.
    /// </summary>
    [Fact]
    public void CollectionEditor_Array_ReadOnlyPropertyAllowsOnlyInPlaceChanges()
    {
        using var source = TestHelpers.PersonSource(out var person);
        var original = person.Codes;
        var session = new PropertyEditSession(source, PropertyEditMode.Staged);
        var editor = session.BeginCollectionEdit(TestHelpers.Item(source, "Codes"));

        Assert.False(editor.CanResize);
        Assert.True(editor.CanReorder);
        Assert.False(editor.Add("c").Succeeded);
        Assert.False(editor.RemoveAt(0).Succeeded);
        Assert.True(editor.Move(0, 1).Succeeded);
        Assert.True(session.Commit().Succeeded);

        Assert.Same(original, person.Codes);
        Assert.Equal(["b", "a"], person.Codes);
    }

    /// <summary>
    ///     Resizing an array behind a settable property assigns a new array of the new length.
    /// </summary>
    [Fact]
    public void CollectionEditor_Array_SettablePropertyReceivesANewArray()
    {
        using var source = TestHelpers.PersonSource(out var person);
        person.Scores = [1, 2];
        var original = person.Scores;
        var session = new PropertyEditSession(source, PropertyEditMode.Staged);
        var editor = session.BeginCollectionEdit(TestHelpers.Item(source, "Scores"));

        Assert.True(editor.CanResize);
        editor.Add(3);
        session.Commit();

        Assert.NotSame(original, person.Scores);
        Assert.Equal([1, 2, 3], person.Scores);
    }

    /// <summary>
    ///     A read-only list cannot be changed in any way.
    /// </summary>
    [Fact]
    public void CollectionEditor_ReadOnlyList_RejectsEveryChange()
    {
        using var source = TestHelpers.PersonSource(out var person);
        var session = new PropertyEditSession(source, PropertyEditMode.Staged);
        var editor = session.BeginCollectionEdit(TestHelpers.Item(source, "History"));

        Assert.False(editor.CanResize);
        Assert.False(editor.CanReorder);
        var add = editor.Add("z");
        Assert.Contains("read-only", add.Error);
        Assert.False(editor.Move(0, 1).Succeeded);
        Assert.False(editor.SetItem(0, "q").Succeeded);
        Assert.False(session.HasChanges);
        Assert.Equal(["x", "y"], person.History);
    }

    /// <summary>
    ///     An <c>ICollection&lt;T&gt;</c> such as a set is cleared and refilled in place.
    /// </summary>
    [Fact]
    public void CollectionEditor_GenericCollection_IsRefilledInPlace()
    {
        using var source = TestHelpers.PersonSource(out var person);
        person.Labels.Add("one");
        var original = person.Labels;
        var session = new PropertyEditSession(source, PropertyEditMode.Staged);
        var editor = session.BeginCollectionEdit(TestHelpers.Item(source, "Labels"));

        editor.Add("two");
        editor.RemoveAt(0);
        session.Commit();

        Assert.Same(original, person.Labels);
        Assert.Equal(["two"], person.Labels);
    }

    /// <summary>
    ///     A null collection property is replaced by a new list when elements are added.
    /// </summary>
    [Fact]
    public void CollectionEditor_NullCollection_IsReplacedByANewList()
    {
        using var source = TestHelpers.PersonSource(out var person);
        person.Tags = null!;
        var session = new PropertyEditSession(source, PropertyEditMode.Staged);
        var editor = session.BeginCollectionEdit(TestHelpers.Item(source, "Tags"));

        Assert.Equal(0, editor.Count);
        editor.Add();
        Assert.True(session.Commit().Succeeded);

        Assert.Single(person.Tags);
    }

    /// <summary>
    ///     Bad indices, null elements for value types and elements of the wrong type are rejected with messages.
    /// </summary>
    [Fact]
    public void CollectionEditor_InvalidArguments_AreRejected()
    {
        using var source = TestHelpers.PersonSource(out var person);
        person.Scores = [1, 2];
        var session = new PropertyEditSession(source, PropertyEditMode.Staged);
        var scores = session.BeginCollectionEdit(TestHelpers.Item(source, "Scores"));

        Assert.False(scores.Insert(5, 1).Succeeded);
        Assert.False(scores.RemoveAt(-1).Succeeded);
        Assert.False(scores.SetItem(2, 1).Succeeded);
        Assert.False(scores.Move(0, 2).Succeeded);
        Assert.True(scores.Move(1, 1).Succeeded);
        Assert.Equal("A Int32 element cannot be empty.", scores.Add(null).Error);
        Assert.False(scores.Add("text").Succeeded);
        Assert.False(session.HasChanges);
        Assert.Throws<ArgumentException>(() => session.BeginCollectionEdit(TestHelpers.Item(source, "Name")));
    }

    /// <summary>
    ///     A later value edit of the collection property replaces the pending collection edit and ends its editor.
    /// </summary>
    [Fact]
    public void CollectionEditor_Staged_ValueEditReplacesThePendingCollectionEdit()
    {
        using var source = TestHelpers.PersonSource(out var person);
        var session = new PropertyEditSession(source, PropertyEditMode.Staged);
        var scores = TestHelpers.Item(source, "Scores");
        var editor = session.BeginCollectionEdit(scores);
        editor.Add(1);

        session.SetValue(scores, ReplacementScores);

        Assert.False(editor.IsActive);
        Assert.Equal(PendingEditKind.Value, Assert.Single(session.GetPendingEdits()).Kind);
        session.Commit();
        Assert.Equal([7, 8], person.Scores);
    }

    /// <summary>
    ///     Element property edits and structural edits can be staged together and are applied in the order recorded.
    /// </summary>
    [Fact]
    public void CollectionEditor_Staged_ElementEditsAndStructuralEditsCommitTogether()
    {
        using var source = TestHelpers.PersonSource(out var person);
        person.Tags.Add(new Tag { Label = "red" });
        var session = new PropertyEditSession(source, PropertyEditMode.Staged);
        session.SetText(session.FindItem("Tags[0].Label")!, "blue");
        var editor = session.BeginCollectionEdit(TestHelpers.Item(source, "Tags"));
        editor.Add(new Tag { Label = "green" });

        var result = session.Commit();

        Assert.True(result.Succeeded);
        Assert.Equal(2, result.Applied);
        Assert.Equal(["blue", "green"], person.Tags.Select(t => t.Label));
    }

    /// <summary>
    ///     In live mode each collection change is written at once and the working copy follows the target.
    /// </summary>
    [Fact]
    public void CollectionEditor_Live_ChangesApplyImmediately()
    {
        using var source = TestHelpers.PersonSource(out var person);
        var session = new PropertyEditSession(source);
        var editor = session.BeginCollectionEdit(TestHelpers.Item(source, "Notes"));

        Assert.True(editor.Add("a").Succeeded);
        Assert.Equal(["a"], person.Notes);
        Assert.True(editor.Add("b").Succeeded);
        Assert.True(editor.Move(1, 0).Succeeded);

        Assert.Equal(["b", "a"], person.Notes);
        Assert.Equal(["b", "a"], editor.Items.Cast<string>());
        Assert.False(session.HasChanges);
    }

    /// <summary>
    ///     A live collection change that fails reports the error and the working copy is re-read from the target.
    /// </summary>
    [Fact]
    public void CollectionEditor_Live_FailureRestoresTheWorkingCopy()
    {
        var store = new List<int> { 1 };
        var item = new CustomPropertyItem("Values", typeof(Collection<int>), () => new FrozenCollection(store));
        var session = new PropertyEditSession(new CustomPropertySource([item]));
        var editor = session.BeginCollectionEdit(item);

        var result = editor.Add(2);

        Assert.False(result.Succeeded);
        Assert.Equal("The list is frozen.", result.Error);
        Assert.Equal([1], editor.Items.Cast<int>());
        Assert.Equal("The list is frozen.", session.GetError(item));
    }
}

/// <summary>
///     A collection that refuses to be changed, to test failures while applying a collection edit.
/// </summary>
public sealed class FrozenCollection : Collection<int>
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="FrozenCollection"/> class.
    /// </summary>
    /// <param name="values">The initial values.</param>
    public FrozenCollection(IEnumerable<int> values)
        : base(values.ToList())
    {
    }

    /// <inheritdoc />
    protected override void ClearItems() => throw new InvalidOperationException("The list is frozen.");

    /// <inheritdoc />
    protected override void InsertItem(int index, int item) => throw new InvalidOperationException("The list is frozen.");
}
