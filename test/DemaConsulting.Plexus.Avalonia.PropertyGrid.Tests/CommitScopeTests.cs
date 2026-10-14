namespace DemaConsulting.Plexus.Avalonia.PropertyGrid.Tests;

/// <summary>
///     Tests of <see cref="IPropertyCommitScope"/> (a source that brackets commits), <see cref="PropertyCommitRefusedException"/>
///     and the category rank used to order categories.
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
    ///     A composite chains the scopes of its sources, the first source outermost, and runs the action once.
    /// </summary>
    [Fact]
    public void CompositePropertySource_RunCommit_ChainsTheSourceScopes()
    {
        // Arrange
        var log = new List<string>();
        var first = new ScopedSource(log);
        var second = new ScopedSource(log);
        var plain = new CustomPropertySource();
        using var composite = new CompositePropertySource(first, plain, second);

        // Act
        composite.RunCommit(() => log.Add("apply"));

        // Assert
        Assert.Equal(["source-before", "source-before", "apply", "source-after", "source-after"], log);
        Assert.Throws<ArgumentNullException>(() => composite.RunCommit(null!));
    }

    /// <summary>
    ///     A category with a lower rank is shown first; within a rank the order is alphabetical, and a category ranks as its lowest item.
    /// </summary>
    [Fact]
    public void PropertyOrdering_Group_OrdersCategoriesByRankThenAlphabetically()
    {
        // Arrange
        PropertyItem Make(string name, string category, int order) =>
            new CustomPropertyItem(name, typeof(string), () => string.Empty, null, new CustomPropertyOptions { Category = category, CategoryOrder = order });
        var items = new[]
        {
            Make("A", "Alpha", 0),
            Make("B", "Zulu", -1),
            Make("C", "Mike", 0),
            Make("D", "Zulu", 5),
        };

        // Act
        var groups = PropertyOrdering.Group(items, PropertySortMode.Categorized);

        // Assert
        Assert.Equal(["Zulu", "Alpha", "Mike"], groups.Select(g => g.Category));
        Assert.Equal(["B", "D"], groups[0].Items.Select(i => i.Name));
        Assert.Equal(0, new CustomPropertyOptions().CategoryOrder);
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
