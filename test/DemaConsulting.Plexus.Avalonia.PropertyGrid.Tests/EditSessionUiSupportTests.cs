namespace DemaConsulting.Plexus.Avalonia.PropertyGrid.Tests;

/// <summary>
///     Tests of the session and collection editor members that the UI layer relies on: replacing the whole working
///     copy and reporting an editor's own rejection of input.
/// </summary>
public class EditSessionUiSupportTests
{
    /// <summary>
    ///     Replacing the working copy in a live session writes once, inside one commit scope call.
    /// </summary>
    [Fact]
    public void CollectionEditor_SetItems_Live_WritesOnceInsideScope()
    {
        var person = new Person { Tags = [new Tag { Label = "a" }] };
        var scopes = 0;
        using var source = new ReflectionPropertySource(person);
        var session = new PropertyEditSession(source, PropertyEditMode.Live, apply =>
        {
            scopes++;
            apply();
        });
        var editor = session.BeginCollectionEdit(session.FindItem("Tags")!);

        var result = editor.SetItems([new Tag { Label = "x" }, new Tag { Label = "y" }]);

        Assert.True(result.Succeeded);
        Assert.Equal(1, scopes);
        Assert.Equal(["x", "y"], person.Tags.Select(t => t.Label));
        Assert.Equal(2, editor.Count);
    }

    /// <summary>
    ///     Replacing the working copy in a staged session records one pending edit that commit applies.
    /// </summary>
    [Fact]
    public void CollectionEditor_SetItems_Staged_RecordsPendingEdit()
    {
        var person = new Person { Tags = [new Tag { Label = "a" }] };
        using var source = new ReflectionPropertySource(person);
        var session = new PropertyEditSession(source, PropertyEditMode.Staged);
        var tags = session.FindItem("Tags")!;

        var result = session.BeginCollectionEdit(tags).SetItems([new Tag { Label = "x" }]);

        Assert.True(result.Succeeded);
        Assert.Equal(["a"], person.Tags.Select(t => t.Label));
        Assert.True(session.IsPending(tags));
        Assert.True(session.Commit().Succeeded);
        Assert.Equal(["x"], person.Tags.Select(t => t.Label));
    }

    /// <summary>
    ///     A fixed-size collection accepts a reordering but refuses a different number of elements; elements of the wrong
    ///     type are refused.
    /// </summary>
    [Fact]
    public void CollectionEditor_SetItems_ValidatesSizeAndElementType()
    {
        var person = new Person();
        using var source = new ReflectionPropertySource(person);
        var session = new PropertyEditSession(source);
        var codes = session.BeginCollectionEdit(session.FindItem("Codes")!);
        var tags = session.BeginCollectionEdit(session.FindItem("Tags")!);

        Assert.False(codes.SetItems(["only"]).Succeeded);
        Assert.True(codes.SetItems(["b", "a"]).Succeeded);
        Assert.Equal(["b", "a"], person.Codes);
        Assert.False(tags.SetItems([42]).Succeeded);
        Assert.Throws<ArgumentNullException>(() => tags.SetItems(null!));
    }

    /// <summary>
    ///     An editor can record its own rejection of input as the item's error, which clears when the edit is discarded.
    /// </summary>
    [Fact]
    public void PropertyEditSession_ReportError_RecordsAndClearsError()
    {
        using var source = new ReflectionPropertySource(new Person());
        var session = new PropertyEditSession(source);
        var name = session.FindItem("Name")!;
        var changes = new List<PropertySessionChangeKind>();
        session.Changed += (_, e) => changes.Add(e.Kind);

        var result = session.ReportError(name, "Not a valid name.");

        Assert.False(result.Succeeded);
        Assert.Equal("Not a valid name.", session.GetError(name));
        Assert.True(session.HasErrors);
        Assert.Equal([PropertySessionChangeKind.ErrorChanged], changes);

        session.Discard(name);

        Assert.False(session.HasErrors);
        Assert.Throws<ArgumentNullException>(() => session.ReportError(null!, "x"));
        Assert.Throws<ArgumentNullException>(() => session.ReportError(name, null!));
    }
}
