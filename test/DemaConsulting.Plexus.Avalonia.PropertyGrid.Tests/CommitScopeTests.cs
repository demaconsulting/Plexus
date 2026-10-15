namespace DemaConsulting.Plexus.Avalonia.PropertyGrid.Tests;

/// <summary>
///     Tests of <see cref="IPropertyCommitScope"/> (a source that brackets commits) and <see cref="PropertyCommitRefusedException"/>.
/// </summary>
public class CommitScopeTests
{
    /// <summary>
    ///     A source over one string value that records what its commit scope does.
    /// </summary>
    private sealed class ScopedSource : IPropertySource, IPropertyCommitScope
    {
        /// <summary>The underlying items.</summary>
        private readonly CustomPropertySource _inner;

        /// <summary>
        ///     Initializes a new instance of the <see cref="ScopedSource"/> class.
        /// </summary>
        /// <param name="log">Where the steps are recorded.</param>
        internal ScopedSource(List<string> log)
        {
            Log = log;
            _inner = new CustomPropertySource([new CustomPropertyItem("Title", typeof(string), () => Value, v =>
            {
                Value = (string)v!;
                log.Add("write");
            })]);
        }

        /// <inheritdoc />
        public event EventHandler<PropertySourceChangedEventArgs>? Changed
        {
            add => _inner.Changed += value;
            remove => _inner.Changed -= value;
        }

        /// <summary>Gets or sets the value.</summary>
        internal string Value { get; set; } = "old";

        /// <summary>Gets the recorded steps.</summary>
        internal List<string> Log { get; }

        /// <summary>Gets or sets what the scope does with the action.</summary>
        internal Action<Action>? Behavior { get; set; }

        /// <inheritdoc />
        public IReadOnlyList<PropertyItem> GetItems() => _inner.GetItems();

        /// <inheritdoc />
        public void RunCommit(Action apply)
        {
            Log.Add("source-before");
            (Behavior ?? (run => run()))(apply);
            Log.Add("source-after");
        }
    }

    /// <summary>
    ///     The source's scope runs around the host's scope, and a staged commit runs it once for all edits.
    /// </summary>
    [Fact]
    public void PropertyEditSession_Commit_SourceScopeIsOutsideTheHostScope()
    {
        // Arrange
        var log = new List<string>();
        var source = new ScopedSource(log);
        var session = new PropertyEditSession(source, PropertyEditMode.Staged, apply =>
        {
            log.Add("host-before");
            apply();
            log.Add("host-after");
        });
        session.SetText(source.GetItems()[0], "new");

        // Act
        var result = session.Commit();

        // Assert
        Assert.True(result.Succeeded);
        Assert.Equal("new", source.Value);
        Assert.Equal(["source-before", "host-before", "write", "host-after", "source-after"], log);
    }

    /// <summary>
    ///     Live edits run the source's scope once per edit, also without a host scope.
    /// </summary>
    [Fact]
    public void PropertyEditSession_LiveEdit_RunsTheSourceScopePerEdit()
    {
        // Arrange
        var log = new List<string>();
        var source = new ScopedSource(log);
        var session = new PropertyEditSession(source, PropertyEditMode.Live);

        // Act
        session.SetText(source.GetItems()[0], "one");
        session.SetText(source.GetItems()[0], "two");

        // Assert
        Assert.Equal(["source-before", "write", "source-after", "source-before", "write", "source-after"], log);
    }

    /// <summary>
    ///     A scope that refuses because of a property marks that property's row and commits nothing.
    /// </summary>
    [Fact]
    public void PropertyEditSession_Commit_RefusedExceptionMarksTheNamedProperty()
    {
        // Arrange
        var log = new List<string>();
        var source = new ScopedSource(log) { Behavior = _ => throw new PropertyCommitRefusedException("Title", "Not allowed here.") };
        var session = new PropertyEditSession(source, PropertyEditMode.Staged);
        var item = source.GetItems()[0];
        session.SetText(item, "new");

        // Act
        var result = session.Commit();

        // Assert
        Assert.False(result.Succeeded);
        Assert.Equal("Not allowed here.", result.Error);
        Assert.Equal("Not allowed here.", session.GetError(item));
        Assert.Equal("old", source.Value);
        Assert.True(session.HasChanges);

        // The failing row is listed with its path and message, and nothing counts as applied
        Assert.Equal(0, result.Applied);
        var failure = Assert.Single(result.Failed);
        Assert.Equal(("Title", "Not allowed here."), (failure.Path, failure.Error));
        Assert.Same(item, failure.Item);
    }

    /// <summary>
    ///     A refusal naming a property that was not edited is still listed in the failed rows, as a transient edit of that item.
    /// </summary>
    [Fact]
    public void PropertyEditSession_Commit_RefusedExceptionForAnUneditedRowIsListedAsFailed()
    {
        // Arrange: two rows, only one edited, the scope refuses the other
        var inner = new CustomPropertySource(
        [
            new CustomPropertyItem("First", typeof(string), () => "a", _ => { }),
            new CustomPropertyItem("Second", typeof(string), () => "b", _ => { }),
        ]);
        var source = new RefusingSource(inner, "Second");
        var session = new PropertyEditSession(source, PropertyEditMode.Staged);
        session.SetText(inner.GetItems()[0], "changed");

        // Act
        var result = session.Commit();

        // Assert
        Assert.False(result.Succeeded);
        var failure = Assert.Single(result.Failed);
        Assert.Equal("Second", failure.Path);
        Assert.Equal("Refused: Second", failure.Error);
        Assert.True(session.HasChanges);
    }

    /// <summary>
    ///     A source whose commit scope refuses with a named property.
    /// </summary>
    /// <param name="inner">The items.</param>
    /// <param name="refusedPath">The path to refuse.</param>
    private sealed class RefusingSource(CustomPropertySource inner, string refusedPath) : IPropertySource, IPropertyCommitScope
    {
        /// <inheritdoc />
        public event EventHandler<PropertySourceChangedEventArgs>? Changed
        {
            add => inner.Changed += value;
            remove => inner.Changed -= value;
        }

        /// <inheritdoc />
        public IReadOnlyList<PropertyItem> GetItems() => inner.GetItems();

        /// <inheritdoc />
        public void RunCommit(Action apply)
        {
            apply();
            throw new PropertyCommitRefusedException(refusedPath, "Refused: " + refusedPath);
        }
    }

    /// <summary>
    ///     A source whose commit scope runs the writes and only then refuses, like a scope that validates after applying.
    /// </summary>
    /// <param name="inner">The source that supplies the items.</param>
    /// <param name="refusedPath">The path to refuse.</param>
    private sealed class ApplyThenRefuseSource(IPropertySource inner, string refusedPath) : IPropertySource, IPropertyCommitScope
    {
        /// <inheritdoc />
        public event EventHandler<PropertySourceChangedEventArgs>? Changed
        {
            add => inner.Changed += value;
            remove => inner.Changed -= value;
        }

        /// <inheritdoc />
        public IReadOnlyList<PropertyItem> GetItems() => inner.GetItems();

        /// <inheritdoc />
        public void RunCommit(Action apply)
        {
            apply();
            throw new PropertyCommitRefusedException(refusedPath, "Refused: " + refusedPath);
        }
    }

    /// <summary>
    ///     A scope that writes and then refuses must not leave any write behind: values, a collection and a reset are restored.
    /// </summary>
    [Fact]
    public void PropertyEditSession_Commit_RefusalAfterWrites_RestoresTheTargetAndKeepsEdits()
    {
        // Arrange: three staged edits (two values and a collection) on a plain target, and a scope that applies then refuses
        using var inner = TestHelpers.PersonSource(out var person);
        person.Name = "Ann";
        person.Age = 30;
        person.Notes.Add("old");
        var source = new ApplyThenRefuseSource(inner, "Name");
        var session = new PropertyEditSession(source, PropertyEditMode.Staged);
        session.SetText(TestHelpers.Item(source, "Name"), "Zed");
        session.SetText(TestHelpers.Item(source, "Age"), "9");
        var editor = session.BeginCollectionEdit(TestHelpers.Item(source, "Notes"));
        editor.Add("new");

        // Act
        var result = session.Commit();

        // Assert: the documented atomic contract - nothing applied, target untouched, every edit still pending
        Assert.False(result.Succeeded);
        Assert.Equal(0, result.Applied);
        Assert.Equal("Name", Assert.Single(result.Failed).Path);
        Assert.Equal("Refused: Name", result.Error);
        Assert.Equal("Ann", person.Name);
        Assert.Equal(30, person.Age);
        Assert.Equal(["old"], person.Notes);
        Assert.Equal(3, session.GetPendingEdits().Count);
        Assert.True(session.HasChanges);
    }

    /// <summary>
    ///     An edit whose setter changes the target and then throws is restored too when a later edit makes the scope refuse.
    /// </summary>
    [Fact]
    public void PropertyEditSession_Commit_RefusalAfterPartialFailedWrite_RestoresTheTarget()
    {
        // Arrange: the first setter writes its field and then throws; the scope refuses the second edit after the writes ran
        var partial = "old";
        var other = "b";
        var first = new CustomPropertyItem("First", typeof(string), () => partial, v =>
        {
            partial = (string)v!;
            if (partial == "half-written")
            {
                throw new InvalidOperationException("rejected after the write");
            }
        });
        var second = new CustomPropertyItem("Second", typeof(string), () => other, v => other = (string)v!);
        var source = new ApplyThenRefuseSource(new CustomPropertySource([first, second]), "Second");
        var session = new PropertyEditSession(source, PropertyEditMode.Staged);
        session.SetText(first, "half-written");
        session.SetText(second, "changed");

        // Act
        var result = session.Commit();

        // Assert: nothing applied, the partial write of the failed edit is undone with the rest, both edits still pending
        Assert.False(result.Succeeded);
        Assert.Equal(0, result.Applied);
        Assert.Equal("Refused: Second", result.Error);
        Assert.Equal("old", partial);
        Assert.Equal("b", other);
        Assert.Equal(2, session.GetPendingEdits().Count);
        Assert.True(session.HasChanges);
    }

    /// <summary>
    ///     A scope that already rolled the writes back by itself leaves nothing to restore: no extra write happens.
    /// </summary>
    [Fact]
    public void PropertyEditSession_Commit_RefusalAfterScopeRolledBack_WritesNothingAgain()
    {
        // Arrange: a scope that applies, undoes its own write and then refuses
        var log = new List<string>();
        var source = new ScopedSource(log);
        source.Behavior = apply =>
        {
            apply();
            source.Value = "old";
            log.Add("rolled-back");
            throw new PropertyCommitRefusedException("Title", "Not allowed here.");
        };
        var session = new PropertyEditSession(source, PropertyEditMode.Staged);
        session.SetText(source.GetItems()[0], "new");

        // Act
        var result = session.Commit();

        // Assert: one write only (the original), no restore write, value unchanged
        Assert.False(result.Succeeded);
        Assert.Equal("old", source.Value);
        Assert.Equal(1, log.Count(entry => entry == "write"));
        Assert.True(session.HasChanges);
    }

    /// <summary>
    ///     A value that cannot be put back after a refusal is named in the commit error.
    /// </summary>
    [Fact]
    public void PropertyEditSession_Commit_RefusalAfterWrites_ReportsAValueThatCouldNotBeRestored()
    {
        // Arrange: a setter that accepts the first write only
        var writes = 0;
        var value = "old";
        var item = new CustomPropertyItem("Title", typeof(string), () => value, v =>
        {
            if (++writes > 1)
            {
                throw new InvalidOperationException("locked");
            }

            value = (string)v!;
        });
        var source = new ApplyThenRefuseSource(new CustomPropertySource([item]), "Title");
        var session = new PropertyEditSession(source, PropertyEditMode.Staged);
        session.SetText(item, "new");

        // Act
        var result = session.Commit();

        // Assert
        Assert.False(result.Succeeded);
        Assert.Equal("Refused: Title The previous value of 'Title' could not be restored.", result.Error);
        Assert.Equal(0, result.Applied);
    }

    /// <summary>
    ///     A setter that rejects every value the way framework code does, with an argument exception that names its parameter.
    /// </summary>
    /// <param name="value">The value.</param>
    private static void RejectValue(object? value) => throw new ArgumentException($"{value} is not allowed.", nameof(value));

    /// <summary>
    ///     Error messages produced from argument exceptions never carry the parameter name suffix of the framework.
    /// </summary>
    [Fact]
    public void PropertyEditSession_SetText_ArgumentExceptionMessage_HasNoParameterSuffix()
    {
        // Arrange: a setter that throws an ArgumentException the way framework code does
        var item = new CustomPropertyItem("Count", typeof(int), () => 1, RejectValue);
        var session = new PropertyEditSession(new CustomPropertySource([item]), PropertyEditMode.Live);

        // Act
        var result = session.SetText(item, "5");
        var parse = session.SetText(item, "abc");

        // Assert: the reason is clean user-facing text
        Assert.Equal("5 is not allowed.", result.Error);
        Assert.False(parse.Succeeded);
        Assert.DoesNotContain("(Parameter", parse.Error, StringComparison.Ordinal);
        Assert.DoesNotContain("(Parameter", session.GetError(item), StringComparison.Ordinal);
    }

    /// <summary>
    ///     A path that contains dots at the root level, as the node source uses, is found as a whole and its nested members after it.
    /// </summary>
    [Fact]
    public void PropertyEditSession_FindItem_PrefixedRootPaths_AreResolvedByTheirFullPath()
    {
        // Arrange: no prefixed items here, so a bare root with a nested path is the reference behavior
        var inner = new CustomPropertySource([new CustomPropertyItem("Title", typeof(string), () => "t", _ => { })]);
        var session = new PropertyEditSession(inner, PropertyEditMode.Live);

        // Act / Assert
        Assert.Same(inner.GetItems()[0], session.FindItem("Title"));
        Assert.Null(session.FindItem("Title.Missing"));
        Assert.Null(session.FindItem("Other"));
        Assert.Null(session.FindItem(string.Empty));
    }

    /// <summary>
    ///     In live mode a refusal is shown on the edited row.
    /// </summary>
    [Fact]
    public void PropertyEditSession_LiveEdit_RefusedExceptionIsAnInlineError()
    {
        // Arrange
        var source = new ScopedSource([]) { Behavior = _ => throw new PropertyCommitRefusedException("Title", "Not allowed here.") };
        var session = new PropertyEditSession(source, PropertyEditMode.Live);

        // Act
        var result = session.SetText(source.GetItems()[0], "new");

        // Assert
        Assert.False(result.Succeeded);
        Assert.Equal("Not allowed here.", session.GetError(source.GetItems()[0]));
    }

    /// <summary>
    ///     A scope that never runs the action is reported, as for a host scope.
    /// </summary>
    [Fact]
    public void PropertyEditSession_Commit_SourceScopeThatSkipsTheActionFails()
    {
        // Arrange
        var source = new ScopedSource([]) { Behavior = _ => { } };
        var session = new PropertyEditSession(source, PropertyEditMode.Staged);
        session.SetText(source.GetItems()[0], "new");

        // Act
        var result = session.Commit();

        // Assert
        Assert.False(result.Succeeded);
        Assert.Equal("The commit scope did not apply the changes.", result.Error);
    }

    /// <summary>
    ///     The refusal exception carries the path and requires one.
    /// </summary>
    [Fact]
    public void PropertyCommitRefusedException_Constructor_KeepsPathAndMessage()
    {
        // Act
        var exception = new PropertyCommitRefusedException("Name", "Taken.");

        // Assert
        Assert.Equal("Name", exception.Path);
        Assert.Equal("Taken.", exception.Message);
        Assert.Throws<ArgumentException>(() => new PropertyCommitRefusedException(" ", "x"));
    }
}
