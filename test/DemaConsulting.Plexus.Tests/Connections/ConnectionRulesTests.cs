namespace DemaConsulting.Plexus.Tests;

/// <summary>
///     Unit tests that call the <see cref="ConnectionRules"/> validators and <see cref="ConnectionCheck"/> results directly.
/// </summary>
public class ConnectionRulesTests
{
    /// <summary>
    ///     Creates a page with a producer, a float consumer and an integer consumer.
    /// </summary>
    /// <returns>The page and the three nodes.</returns>
    private static (DiagramPage Page, Node Producer, Node Consumer, Node IntConsumer) Create()
    {
        var page = TestModel.CreateDiagram().Pages.Add("Main");
        return (
            page,
            page.AddNode("acme.Producer", 0, 0),
            page.AddNode("acme.Consumer", 300, 0),
            page.AddNode("acme.IntConsumer", 300, 150));
    }

    /// <summary>
    ///     Validates that the default rule allows any pair of free ports, whatever their types.
    /// </summary>
    [Fact]
    public void ConnectionRules_Default_AnyPorts_ReturnsAllowed()
    {
        // Arrange: free ports of differing types
        var (_, producer, consumer, intConsumer) = Create();

        // Act: ask the default rule directly
        var matching = ConnectionRules.Default(producer.Port("value"), consumer.Port("in"));
        var mismatched = ConnectionRules.Default(producer.Port("value"), intConsumer.Port("in"));

        // Assert: nothing is denied and no reason is reported
        Assert.True(matching.IsAllowed);
        Assert.True(mismatched.IsAllowed);
        Assert.Null(matching.Reason);
    }

    /// <summary>
    ///     Validates that the SameType rule names both port type keys in its denial reason.
    /// </summary>
    [Fact]
    public void ConnectionRules_SameType_DirectCall_ReasonNamesBothTypes()
    {
        // Arrange
        var (_, producer, consumer, intConsumer) = Create();

        // Act
        var denied = ConnectionRules.SameType(producer.Port("value"), intConsumer.Port("in"));
        var allowed = ConnectionRules.SameType(producer.Port("value"), consumer.Port("in"));

        // Assert: the reason names the source and target type keys
        Assert.False(denied.IsAllowed);
        Assert.Contains("'f32'", denied.Reason, StringComparison.Ordinal);
        Assert.Contains("'i32'", denied.Reason, StringComparison.Ordinal);
        Assert.True(allowed.IsAllowed);
    }

    /// <summary>
    ///     Validates that the SingleInputWire rule names the occupied input in its denial reason.
    /// </summary>
    [Fact]
    public void ConnectionRules_SingleInputWire_DirectCall_ReasonNamesInputAndNode()
    {
        // Arrange: an input that already has a wire
        var (page, producer, consumer, _) = Create();
        var free = ConnectionRules.SingleInputWire(producer.Port("value"), consumer.Port("in"));
        page.Connect(producer.Port("value"), consumer.Port("in"));

        // Act
        var occupied = ConnectionRules.SingleInputWire(producer.Port("value"), consumer.Port("in"));

        // Assert: free input is allowed; occupied input is denied with its names
        Assert.True(free.IsAllowed);
        Assert.False(occupied.IsAllowed);
        Assert.Contains($"'{consumer.Name}.in'", occupied.Reason, StringComparison.Ordinal);
        Assert.Contains("does not accept more than one", occupied.Reason, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Validates that the default rule is the single-input rule: a second wire into an input is refused with a clear reason.
    /// </summary>
    [Fact]
    public void ConnectionRules_Default_OccupiedInput_DeniesWithReason()
    {
        // Arrange: an input that already has a wire
        var (page, producer, consumer, _) = Create();
        var second = page.AddNode("acme.Producer", 0, 150);
        page.Connect(producer.Port("value"), consumer.Port("in"));

        // Act
        var result = ConnectionRules.Default(second.Port("value"), consumer.Port("in"));

        // Assert
        Assert.False(result.IsAllowed);
        Assert.Equal($"Input '{consumer.Name}.in' already has a wire; it does not accept more than one.", result.Reason);
    }

    /// <summary>
    ///     Validates that an input that allows fan-in passes both the default and the single-input rules however many wires it has.
    /// </summary>
    [Fact]
    public void ConnectionRules_DefaultAndSingleInputWire_OptedInInput_AllowAnotherWire()
    {
        // Arrange: a merging input with a wire already
        var (page, producer, _, _) = Create();
        var merger = page.AddNode("acme.Merger", 300, 300);
        var second = page.AddNode("acme.Producer", 0, 300);
        page.Connect(producer.Port("value"), merger.Port("in"));

        // Act
        var byDefault = ConnectionRules.Default(second.Port("value"), merger.Port("in"));
        var single = ConnectionRules.SingleInputWire(second.Port("value"), merger.Port("in"));

        // Assert
        Assert.True(byDefault.IsAllowed);
        Assert.True(single.IsAllowed);
    }

    /// <summary>
    ///     Validates that a rule combined without the default does not carry the single-input restriction, while one that includes it does.
    /// </summary>
    [Fact]
    public void ConnectionRules_All_WithAndWithoutDefault_FanInFollowsTheDefault()
    {
        // Arrange: an occupied plain input
        var (page, producer, consumer, _) = Create();
        var second = page.AddNode("acme.Producer", 0, 150);
        page.Connect(producer.Port("value"), consumer.Port("in"));

        // Act
        var without = ConnectionRules.All(ConnectionRules.SameType)(second.Port("value"), consumer.Port("in"));
        var with = ConnectionRules.All(ConnectionRules.Default, ConnectionRules.SameType)(second.Port("value"), consumer.Port("in"));

        // Assert
        Assert.True(without.IsAllowed);
        Assert.False(with.IsAllowed);
    }

    /// <summary>
    ///     Validates that a combined rule stops evaluating at the first denial.
    /// </summary>
    [Fact]
    public void ConnectionRules_All_FirstDenial_SkipsLaterRules()
    {
        // Arrange: a denying rule followed by a rule that records whether it ran
        var (_, producer, consumer, _) = Create();
        var laterRuleRan = false;
        var combined = ConnectionRules.All(
            (_, _) => ConnectionCheck.Denied("first"),
            (_, _) =>
            {
                laterRuleRan = true;
                return ConnectionCheck.Allowed;
            });

        // Act
        var result = combined(producer.Port("value"), consumer.Port("in"));

        // Assert: the first reason wins and the later rule never ran
        Assert.False(result.IsAllowed);
        Assert.Equal("first", result.Reason);
        Assert.False(laterRuleRan);
    }

    /// <summary>
    ///     Validates that a combination of no rules allows every connection.
    /// </summary>
    [Fact]
    public void ConnectionRules_All_NoRules_ReturnsAllowed()
    {
        // Arrange
        var (_, producer, consumer, _) = Create();

        // Act
        var result = ConnectionRules.All()(producer.Port("value"), consumer.Port("in"));

        // Assert
        Assert.True(result.IsAllowed);
    }

    /// <summary>
    ///     Validates that changing the caller's array after combining does not change the combined rule.
    /// </summary>
    [Fact]
    public void ConnectionRules_All_ArrayChangedAfterCombining_KeepsOriginalRules()
    {
        // Arrange: combine an allowing rule, then replace it in the caller's array with a denying rule
        var (_, producer, consumer, _) = Create();
        var rules = new ConnectionValidator[] { ConnectionRules.Default };
        var combined = ConnectionRules.All(rules);
        rules[0] = (_, _) => ConnectionCheck.Denied("changed");

        // Act
        var result = combined(producer.Port("value"), consumer.Port("in"));

        // Assert: the combined rule still uses the original rule
        Assert.True(result.IsAllowed);
    }

    /// <summary>
    ///     Validates that an allowed result carries no reason.
    /// </summary>
    [Fact]
    public void ConnectionCheck_Allowed_HasNoReason()
    {
        // Act
        var check = ConnectionCheck.Allowed;

        // Assert
        Assert.True(check.IsAllowed);
        Assert.Null(check.Reason);
    }

    /// <summary>
    ///     Validates that a denial keeps its reason text.
    /// </summary>
    [Fact]
    public void ConnectionCheck_Denied_WithReason_KeepsReason()
    {
        // Act
        var check = ConnectionCheck.Denied("Not now.");

        // Assert
        Assert.False(check.IsAllowed);
        Assert.Equal("Not now.", check.Reason);
    }

    /// <summary>
    ///     Validates that a denial created without a reason still carries a default reason.
    /// </summary>
    [Fact]
    public void ConnectionCheck_Denied_NullReason_GetsDefaultReason()
    {
        // Act
        var check = ConnectionCheck.Denied(null!);

        // Assert: denied with a non-blank explanation
        Assert.False(check.IsAllowed);
        Assert.False(string.IsNullOrWhiteSpace(check.Reason));
    }

    /// <summary>
    ///     Validates that a denial created with a blank reason gets the default reason instead.
    /// </summary>
    [Fact]
    public void ConnectionCheck_Denied_BlankReason_GetsDefaultReason()
    {
        // Act
        var check = ConnectionCheck.Denied("  ");

        // Assert: the blank text is replaced by a readable explanation
        Assert.False(check.IsAllowed);
        Assert.False(string.IsNullOrWhiteSpace(check.Reason));
    }
}
