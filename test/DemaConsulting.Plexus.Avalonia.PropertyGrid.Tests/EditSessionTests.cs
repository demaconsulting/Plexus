using System.Globalization;

namespace DemaConsulting.Plexus.Avalonia.PropertyGrid.Tests;

/// <summary>
///     Unit tests of the EditSession unit: live and staged value editing, commit scope, errors and change events.
/// </summary>
public class EditSessionTests
{
    /// <summary>
    ///     In live mode a value is written immediately and nothing is pending.
    /// </summary>
    [Fact]
    public void PropertyEditSession_Live_SetValueWritesImmediately()
    {
        using var source = TestHelpers.PersonSource(out var person);
        var session = new PropertyEditSession(source);
        var events = new List<PropertySessionChangedEventArgs>();
        session.Changed += (_, e) => events.Add(e);

        var result = session.SetValue(TestHelpers.Item(source, "Age"), 5);

        Assert.True(result.Succeeded);
        Assert.Equal(5, person.Age);
        Assert.False(session.HasChanges);
        Assert.False(session.IsPending(TestHelpers.Item(source, "Age")));
        Assert.Equal(PropertyEditMode.Live, session.Mode);
        Assert.Same(source, session.Source);
        var applied = Assert.Single(events);
        Assert.Equal(PropertySessionChangeKind.Applied, applied.Kind);
        Assert.Equal("Age", applied.Path);
        Assert.False(applied.RefreshAll);
    }

    /// <summary>
    ///     In live mode text is converted and an invalid text is recorded as the item's error until a later edit succeeds.
    /// </summary>
    [Fact]
    public void PropertyEditSession_Live_SetTextRecordsAndClearsErrors()
    {
        using var source = TestHelpers.PersonSource(out var person);
        var session = new PropertyEditSession(source);
        var age = TestHelpers.Item(source, "Age");

        var bad = session.SetText(age, "abc", CultureInfo.InvariantCulture);

        Assert.False(bad.Succeeded);
        Assert.Equal(bad.Error, session.GetError(age));
        Assert.True(session.HasErrors);
        Assert.Equal(bad.Error, session.GetErrors()["Age"]);
        Assert.Equal(0, person.Age);

        Assert.True(session.SetText(age, "42", CultureInfo.InvariantCulture).Succeeded);
        Assert.Equal(42, person.Age);
        Assert.Null(session.GetError(age));
        Assert.False(session.HasErrors);
    }

    /// <summary>
    ///     Read-only items, invalid values and throwing setters fail with a message instead of throwing.
    /// </summary>
    [Fact]
    public void PropertyEditSession_Live_ReadOnlyInvalidAndThrowingSettersFail()
    {
        using var source = TestHelpers.PersonSource(out var person);
        var session = new PropertyEditSession(source);

        var readOnly = session.SetValue(TestHelpers.Item(source, "Id"), "x");
        var invalid = session.SetValue(TestHelpers.Item(source, "Percent"), 101);
        var throwing = session.SetValue(TestHelpers.Item(source, "Fragile"), 1);
        var readOnlyText = session.SetText(TestHelpers.Item(source, "Id"), "x");

        Assert.Equal("'Id' is read-only.", readOnly.Error);
        Assert.Equal("Percent must be between 0 and 100.", invalid.Error);
        Assert.Equal("Fragile does not accept values.", throwing.Error);
        Assert.False(readOnlyText.Succeeded);
        Assert.Equal("P-1", person.Id);
        Assert.Equal(3, session.GetErrors().Count);
    }

    /// <summary>
    ///     In live mode every write runs inside the commit scope.
    /// </summary>
    [Fact]
    public void PropertyEditSession_Live_CommitScopeWrapsEachWrite()
    {
        using var source = TestHelpers.PersonSource(out var person);
        var log = new List<string>();
        var session = new PropertyEditSession(source, PropertyEditMode.Live, apply =>
        {
            log.Add($"begin age={person.Age}");
            apply();
            log.Add($"end age={person.Age}");
        });

        session.SetValue(TestHelpers.Item(source, "Age"), 5);
        session.SetValue(TestHelpers.Item(source, "Age"), 6);

        Assert.Equal(["begin age=0", "end age=5", "begin age=5", "end age=6"], log);
    }

    /// <summary>
    ///     A commit scope that does not run the write, or that throws, makes the edit fail without throwing.
    /// </summary>
    [Fact]
    public void PropertyEditSession_Live_CommitScopeFailuresAreReported()
    {
        using var source = TestHelpers.PersonSource(out var person);
        var skipping = new PropertyEditSession(source, PropertyEditMode.Live, _ => { });
        var throwing = new PropertyEditSession(source, PropertyEditMode.Live, _ => throw new InvalidOperationException("no transaction"));

        var skipped = skipping.SetValue(TestHelpers.Item(source, "Age"), 5);
        var failed = throwing.SetValue(TestHelpers.Item(source, "Age"), 6);

        Assert.False(skipped.Succeeded);
        Assert.Equal("The commit scope did not apply the changes.", skipped.Error);
        Assert.Equal("no transaction", failed.Error);
        Assert.Equal(0, person.Age);
    }

    /// <summary>
    ///     Reset restores the default in live mode and fails for items without one.
    /// </summary>
    [Fact]
    public void PropertyEditSession_Live_ResetRestoresTheDefault()
    {
        using var source = TestHelpers.PersonSource(out var person);
        person.Age = 9;
        var session = new PropertyEditSession(source);

        Assert.True(session.Reset(TestHelpers.Item(source, "Age")).Succeeded);
        var nickname = session.Reset(TestHelpers.Item(source, "Nickname"));

        Assert.Equal(0, person.Age);
        Assert.False(nickname.Succeeded);
        Assert.Equal("'Nickname' cannot be reset.", nickname.Error);
    }

    /// <summary>
    ///     Writing a property annotated <c>RefreshProperties.All</c> asks the user interface to re-read everything.
    /// </summary>
    [Fact]
    public void PropertyEditSession_Live_RefreshPropertiesAllRequestsRefresh()
    {
        using var source = TestHelpers.PersonSource(out _);
        var session = new PropertyEditSession(source);
        var events = new List<PropertySessionChangedEventArgs>();
        session.Changed += (_, e) => events.Add(e);

        session.SetValue(TestHelpers.Item(source, "Units"), Unit.Feet);
        session.SetValue(TestHelpers.Item(source, "Name"), "n");

        Assert.True(events[0].RefreshAll);
        Assert.False(events[1].RefreshAll);
    }

    /// <summary>
    ///     In staged mode an edit is buffered without touching the target and the pending value is shown.
    /// </summary>
    [Fact]
    public void PropertyEditSession_Staged_SetValueBuffersWithoutTouchingTheTarget()
    {
        using var source = TestHelpers.PersonSource(out var person);
        var session = new PropertyEditSession(source, PropertyEditMode.Staged);
        var age = TestHelpers.Item(source, "Age");
        var events = new List<PropertySessionChangedEventArgs>();
        session.Changed += (_, e) => events.Add(e);

        Assert.True(session.SetText(age, "37", CultureInfo.InvariantCulture).Succeeded);

        Assert.Equal(0, person.Age);
        Assert.True(session.HasChanges);
        Assert.True(session.IsPending(age));
        Assert.Equal(37, session.GetValue(age));
        Assert.Equal("37", session.GetText(age, CultureInfo.InvariantCulture));
        Assert.Equal(0, session.GetValue(TestHelpers.Item(source, "Percent")));
        var edit = Assert.Single(session.GetPendingEdits());
        Assert.Equal("Age", edit.Path);
        Assert.Equal(PendingEditKind.Value, edit.Kind);
        Assert.Equal(37, edit.Value);
        Assert.Same(age, edit.Item);
        Assert.Null(edit.Error);
        Assert.Equal(PropertySessionChangeKind.PendingChanged, Assert.Single(events).Kind);
    }

    /// <summary>
    ///     Commit writes the pending edits, clears them and reports the count.
    /// </summary>
    [Fact]
    public void PropertyEditSession_Staged_CommitAppliesAndClearsTheEdits()
    {
        using var source = TestHelpers.PersonSource(out var person);
        var session = new PropertyEditSession(source, PropertyEditMode.Staged);
        session.SetValue(TestHelpers.Item(source, "Age"), 37);
        session.SetValue(TestHelpers.Item(source, "Name"), "Ada");
        var events = new List<PropertySessionChangeKind>();
        session.Changed += (_, e) => events.Add(e.Kind);

        var result = session.Commit();

        Assert.True(result.Succeeded);
        Assert.Equal(2, result.Applied);
        Assert.Empty(result.Failed);
        Assert.Null(result.Error);
        Assert.Equal((37, "Ada"), (person.Age, person.Name));
        Assert.False(session.HasChanges);
        Assert.Equal([PropertySessionChangeKind.Committed], events);
    }

    /// <summary>
    ///     Cancel discards every pending edit and error and leaves the target alone.
    /// </summary>
    [Fact]
    public void PropertyEditSession_Staged_CancelDiscardsEverything()
    {
        using var source = TestHelpers.PersonSource(out var person);
        var session = new PropertyEditSession(source, PropertyEditMode.Staged);
        session.SetValue(TestHelpers.Item(source, "Age"), 37);
        session.SetText(TestHelpers.Item(source, "Percent"), "abc");
        PropertySessionChangedEventArgs? last = null;
        session.Changed += (_, e) => last = e;

        session.Cancel();

        Assert.False(session.HasChanges);
        Assert.False(session.HasErrors);
        Assert.Equal(0, person.Age);
        Assert.Equal(PropertySessionChangeKind.Cancelled, last!.Kind);
        Assert.True(last.RefreshAll);
        Assert.Equal(0, session.GetValue(TestHelpers.Item(source, "Age")));
    }

    /// <summary>
    ///     Pending edits are applied in the order they were first made; re-editing a property keeps its position.
    /// </summary>
    [Fact]
    public void PropertyEditSession_Staged_CommitAppliesInFirstEditOrder()
    {
        var log = new List<string>();
        var a = new[] { 0 };
        var b = new[] { 0 };
        var c = new[] { 0 };
        var items = new[] { TestHelpers.LoggedInt("A", a, log), TestHelpers.LoggedInt("B", b, log), TestHelpers.LoggedInt("C", c, log) };
        var session = new PropertyEditSession(new CustomPropertySource(items), PropertyEditMode.Staged);

        session.SetValue(items[1], 1);
        session.SetValue(items[0], 1);
        session.SetValue(items[2], 1);
        session.SetValue(items[1], 2);
        session.Commit();

        Assert.Equal(["B", "A", "C"], log);
        Assert.Equal(2, b[0]);
    }

    /// <summary>
    ///     Setting a property back to its current value removes the pending edit.
    /// </summary>
    [Fact]
    public void PropertyEditSession_Staged_SettingBackToTheCurrentValueRemovesTheEdit()
    {
        using var source = TestHelpers.PersonSource(out _);
        var session = new PropertyEditSession(source, PropertyEditMode.Staged);
        var age = TestHelpers.Item(source, "Age");

        session.SetValue(age, 5);
        Assert.True(session.HasChanges);
        session.SetValue(age, 0);
        Assert.False(session.HasChanges);
        session.SetValue(age, 0);
        Assert.False(session.HasChanges);
    }

    /// <summary>
    ///     Invalid text and values are never buffered; the previous pending edit is kept and the error is recorded.
    /// </summary>
    [Fact]
    public void PropertyEditSession_Staged_ValidationErrorsAreNotBuffered()
    {
        using var source = TestHelpers.PersonSource(out _);
        var session = new PropertyEditSession(source, PropertyEditMode.Staged);
        var percent = TestHelpers.Item(source, "Percent");
        session.SetValue(percent, 10);

        var rangeError = session.SetValue(percent, 500);
        var textError = session.SetText(percent, "abc", CultureInfo.InvariantCulture);
        var readOnly = session.SetValue(TestHelpers.Item(source, "Id"), "x");

        Assert.False(rangeError.Succeeded);
        Assert.False(textError.Succeeded);
        Assert.False(readOnly.Succeeded);
        Assert.Equal(10, session.GetValue(percent));
        Assert.Single(session.GetPendingEdits());
        Assert.Equal(textError.Error, session.GetError(percent));
        Assert.True(session.SetValue(percent, 11).Succeeded);
        Assert.Null(session.GetError(percent));
    }

    /// <summary>
    ///     A failing edit stays pending with its error while the others are applied, and a retry can succeed.
    /// </summary>
    [Fact]
    public void PropertyEditSession_Staged_CommitFailureKeepsTheFailedEditPending()
    {
        using var source = TestHelpers.PersonSource(out var person);
        var session = new PropertyEditSession(source, PropertyEditMode.Staged);
        var fragile = TestHelpers.Item(source, "Fragile");
        session.SetValue(TestHelpers.Item(source, "Age"), 4);
        session.SetValue(fragile, 9);
        session.SetValue(TestHelpers.Item(source, "Name"), "Ada");

        var result = session.Commit();

        Assert.False(result.Succeeded);
        Assert.Equal(2, result.Applied);
        var failed = Assert.Single(result.Failed);
        Assert.Equal("Fragile", failed.Path);
        Assert.Equal("Fragile does not accept values.", failed.Error);
        Assert.Equal((4, "Ada"), (person.Age, person.Name));
        Assert.True(session.IsPending(fragile));
        Assert.Equal("Fragile does not accept values.", session.GetError(fragile));
        Assert.Equal("Fragile does not accept values.", Assert.Single(session.GetPendingEdits()).Error);
        Assert.False(session.Commit().Succeeded);
        session.Discard(fragile);
        Assert.False(session.HasChanges);
        Assert.False(session.HasErrors);
    }

    /// <summary>
    ///     Commit runs all writes inside one scope call, and not at all when nothing is pending.
    /// </summary>
    [Fact]
    public void PropertyEditSession_Staged_CommitScopeIsCalledOnceForAllEdits()
    {
        using var source = TestHelpers.PersonSource(out var person);
        var calls = 0;
        var seenInside = new List<int>();
        var session = new PropertyEditSession(source, PropertyEditMode.Staged, apply =>
        {
            calls++;
            apply();
            seenInside.Add(person.Age);
        });
        Assert.True(session.Commit().Succeeded);
        Assert.Equal(0, calls);
        session.SetValue(TestHelpers.Item(source, "Age"), 3);
        session.SetValue(TestHelpers.Item(source, "Name"), "n");

        session.Commit();

        Assert.Equal(1, calls);
        Assert.Equal([3], seenInside);
    }

    /// <summary>
    ///     A commit scope that throws or does not apply leaves every edit pending and reports the error.
    /// </summary>
    [Fact]
    public void PropertyEditSession_Staged_CommitScopeFailureKeepsAllEditsPending()
    {
        using var source = TestHelpers.PersonSource(out _);
        var throwing = new PropertyEditSession(source, PropertyEditMode.Staged, _ => throw new InvalidOperationException("transaction failed"));
        var skipping = new PropertyEditSession(source, PropertyEditMode.Staged, _ => { });
        throwing.SetValue(TestHelpers.Item(source, "Age"), 3);
        skipping.SetValue(TestHelpers.Item(source, "Age"), 3);

        var failed = throwing.Commit();
        var skipped = skipping.Commit();

        Assert.False(failed.Succeeded);
        Assert.Equal("transaction failed", failed.Error);
        Assert.Equal(0, failed.Applied);
        Assert.True(throwing.HasChanges);
        Assert.Equal("The commit scope did not apply the changes.", skipped.Error);
        Assert.True(skipping.HasChanges);
    }

    /// <summary>
    ///     Nested properties are edited by path: buffered while staged and applied on commit.
    /// </summary>
    [Fact]
    public void PropertyEditSession_Staged_NestedPropertyPathsAreBuffered()
    {
        using var source = TestHelpers.PersonSource(out var person);
        var session = new PropertyEditSession(source, PropertyEditMode.Staged);
        var city = session.FindItem("Address.City")!;
        var width = session.FindItem("Size.Width")!;

        session.SetText(city, "Rome");
        session.SetText(width, "7.5", CultureInfo.InvariantCulture);

        Assert.Equal("Paris", person.Address.City);
        Assert.Equal(0, person.Size.Width);
        Assert.Equal("Rome", session.GetValue(city));
        Assert.Equal(["Address.City", "Size.Width"], session.GetPendingEdits().Select(e => e.Path));

        Assert.True(session.Commit().Succeeded);
        Assert.Equal("Rome", person.Address.City);
        Assert.Equal(7.5, person.Size.Width);
    }

    /// <summary>
    ///     Collection element properties are edited by indexed path.
    /// </summary>
    [Fact]
    public void PropertyEditSession_Staged_CollectionElementPathsAreBuffered()
    {
        using var source = TestHelpers.PersonSource(out var person);
        person.Tags.Add(new Tag { Label = "red" });
        person.Scores = [1, 2, 3];
        var session = new PropertyEditSession(source, PropertyEditMode.Staged);

        session.SetText(session.FindItem("Tags[0].Label")!, "blue");
        session.SetText(session.FindItem("Scores[2]")!, "30");

        Assert.Equal("red", person.Tags[0].Label);
        Assert.Equal(3, person.Scores[2]);
        Assert.Equal("30", session.GetText(session.FindItem("Scores[2]")!, CultureInfo.InvariantCulture));
        Assert.True(session.Commit().Succeeded);
        Assert.Equal("blue", person.Tags[0].Label);
        Assert.Equal(30, person.Scores[2]);
    }

    /// <summary>
    ///     Item paths are resolved through properties, children and collection elements, and bad paths give null.
    /// </summary>
    [Fact]
    public void PropertyEditSession_FindItem_ResolvesPathsAndRejectsBadOnes()
    {
        using var source = TestHelpers.PersonSource(out var person);
        person.Tags.Add(new Tag { Label = "x" });
        var session = new PropertyEditSession(source);

        Assert.Equal("Name", session.FindItem("Name")!.Name);
        Assert.Equal("Address.Street", session.FindItem("Address.Street")!.Path);
        Assert.Equal("Tags[0].Label", session.FindItem("Tags[0].Label")!.Path);
        Assert.Null(session.FindItem("Missing"));
        Assert.Null(session.FindItem("Address.Missing"));
        Assert.Null(session.FindItem("Tags[5]"));
        Assert.Null(session.FindItem("Tags[x]"));
        Assert.Null(session.FindItem("Tags[0"));
        Assert.Null(session.FindItem("[0]"));
        Assert.Null(session.FindItem(".Name"));
        Assert.Throws<ArgumentNullException>(() => session.FindItem(null!));
    }

    /// <summary>
    ///     A staged reset is buffered, shows the default value and is applied on commit.
    /// </summary>
    [Fact]
    public void PropertyEditSession_Staged_ResetIsBufferedAndApplied()
    {
        using var source = TestHelpers.PersonSource(out var person);
        person.Age = 9;
        person.Level = 1;
        var session = new PropertyEditSession(source, PropertyEditMode.Staged);
        var age = TestHelpers.Item(source, "Age");
        var level = TestHelpers.Item(source, "Level");

        Assert.True(session.Reset(age).Succeeded);
        Assert.True(session.Reset(level).Succeeded);
        Assert.False(session.Reset(TestHelpers.Item(source, "Nickname")).Succeeded);

        Assert.Equal(9, person.Age);
        Assert.Equal(0, session.GetValue(age));
        Assert.Equal(1, session.GetValue(level));
        Assert.All(session.GetPendingEdits(), e => Assert.Equal(PendingEditKind.Reset, e.Kind));
        Assert.True(session.Commit().Succeeded);
        Assert.Equal((0, 5), (person.Age, person.Level));
    }

    /// <summary>
    ///     A later value edit replaces a pending reset and keeps its position.
    /// </summary>
    [Fact]
    public void PropertyEditSession_Staged_ValueEditReplacesAPendingReset()
    {
        using var source = TestHelpers.PersonSource(out var person);
        person.Age = 9;
        var session = new PropertyEditSession(source, PropertyEditMode.Staged);
        var age = TestHelpers.Item(source, "Age");
        session.Reset(age);

        session.SetValue(age, 4);

        var edit = Assert.Single(session.GetPendingEdits());
        Assert.Equal(PendingEditKind.Value, edit.Kind);
        Assert.Equal(4, edit.Value);
        session.Commit();
        Assert.Equal(4, person.Age);
    }

    /// <summary>
    ///     Discard removes the edit and error of one item only.
    /// </summary>
    [Fact]
    public void PropertyEditSession_Staged_DiscardRemovesOneItem()
    {
        using var source = TestHelpers.PersonSource(out _);
        var session = new PropertyEditSession(source, PropertyEditMode.Staged);
        var age = TestHelpers.Item(source, "Age");
        var name = TestHelpers.Item(source, "Name");
        session.SetValue(age, 3);
        session.SetValue(name, "n");

        Assert.True(session.Discard(age));
        Assert.False(session.Discard(age));

        Assert.False(session.IsPending(age));
        Assert.True(session.IsPending(name));
    }

    /// <summary>
    ///     Committing a property annotated <c>RefreshProperties.All</c> asks for a full refresh.
    /// </summary>
    [Fact]
    public void PropertyEditSession_Staged_CommitOfRefreshPropertyRequestsRefresh()
    {
        using var source = TestHelpers.PersonSource(out _);
        var session = new PropertyEditSession(source, PropertyEditMode.Staged);
        PropertySessionChangedEventArgs? committed = null;
        session.Changed += (_, e) => committed = e.Kind == PropertySessionChangeKind.Committed ? e : committed;
        session.SetValue(TestHelpers.Item(source, "Units"), Unit.Miles);

        session.Commit();

        Assert.True(committed!.RefreshAll);
    }

    /// <summary>
    ///     A session rejects missing arguments.
    /// </summary>
    [Fact]
    public void PropertyEditSession_Arguments_AreValidated()
    {
        using var source = TestHelpers.PersonSource(out _);
        var session = new PropertyEditSession(source);
        var name = TestHelpers.Item(source, "Name");

        Assert.Throws<ArgumentNullException>(() => new PropertyEditSession(null!));
        Assert.Throws<ArgumentNullException>(() => session.SetValue(null!, 1));
        Assert.Throws<ArgumentNullException>(() => session.SetText(name, null!));
        Assert.Throws<ArgumentNullException>(() => session.GetValue(null!));
        Assert.Throws<ArgumentNullException>(() => session.IsPending(null!));
        Assert.Throws<ArgumentNullException>(() => session.GetError(null!));
        Assert.Throws<ArgumentNullException>(() => session.Reset(null!));
        Assert.Throws<ArgumentNullException>(() => session.Discard(null!));
        Assert.Throws<ArgumentNullException>(() => session.BeginCollectionEdit(null!));
    }
}
