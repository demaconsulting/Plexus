## ConnectionRules Unit Verification Design

This document describes how the ConnectionRules unit (the connection rule delegate, the ready-made rules, rule
combination and the allowed-or-denied result) is verified. Requirements are defined in the ConnectionRules Unit
Requirements document.

### Verification Approach

The unit tests use the real rules and the real Model objects as collaborators; nothing is mocked, because the rules
only read the type key, name, node, wires and fan-in setting of the ports they are given. A small diagram built from the
shared test node types provides the ports. Most tests call the rule delegates directly (`ConnectionRulesTests.cs`). The
tests in `ConnectionTests.cs` assign a rule to a diagram and ask a page whether two ports can be connected, which
proves that a host rule is consulted with the output port first and its reason reaches the caller. Host rules are
written as lambdas inside the tests so the order of arguments and the number of calls can be observed.

### Test Environment

No setup beyond the standard xUnit v3 test runner on .NET. No external services or files are needed.

### Acceptance Criteria

All ConnectionRules unit tests pass. Allowed and denied outcomes match the expected values, denial reasons contain the
documented details, and argument errors raise the documented exceptions.

### Test Scenarios

#### Default Rule (`Plexus-ConnectionRules-DefaultSingleInput`)

- **ConnectionRules_Default_AnyPorts_ReturnsAllowed**: the default rule allows free ports of matching and differing
  types.
- **ConnectionRules_Default_AllowsAndHostRuleReceivesOutputThenInput**: a new diagram uses the default rule, and a rule
  assigned afterwards replaces it.
- **ConnectionRules_Default_OccupiedInput_DeniesWithReason**: a second wire into an input is denied with the documented
  reason text.
- **ConnectionRules_DefaultAndSingleInputWire_OptedInInput_AllowAnotherWire**: an input whose definition allows fan-in
  passes both rules however many wires it has.
- **DiagramPage_Connect_DefaultRules_RefuseFanInAndAllowFanOut**: through a page, `CanConnect` and the `Connect` exception
  carry the same reason, the occupied input keeps its single wire and an output may feed several inputs.
- **DiagramPage_Connect_InputAllowsMultipleWires_AcceptsSeveralWires**: an opted-in input accepts a second wire.
- **DiagramPage_Connect_HostRuleWithoutDefault_AllowsFanInEverywhere**: a host rule assigned without the default does not
  restrict fan-in.

#### Ready-Made Rules (`Plexus-ConnectionRules-SameType`, `Plexus-ConnectionRules-SingleInputWire`)

- **ConnectionRules_SameType_RejectsMismatchedTypes**: a float output and an integer input are denied with a reason
  naming the type; equal types are allowed.
- **ConnectionRules_SameType_DirectCall_ReasonNamesBothTypes**: the denial reason names both type keys.
- **ConnectionRules_SingleInputWire_RejectsSecondWireToInput**: a second wire to an occupied input is denied, while
  another free input is allowed.
- **ConnectionRules_SingleInputWire_DirectCall_ReasonNamesInputAndNode**: a free input is allowed; an occupied input is
  denied with a reason naming the input and its node.
- **ConnectionRules_DefaultAndSingleInputWire_OptedInInput_AllowAnotherWire**: the single-input rule honors the opt-in.

#### Combination (`Plexus-ConnectionRules-Combination`)

- **ConnectionRules_All_CombinesRulesAndReportsFirstDenial**: a combined rule reports the first failing rule's reason
  and rejects null rules and null elements.
- **ConnectionRules_All_FirstDenial_SkipsLaterRules**: rules after the first denial are not evaluated.
- **ConnectionRules_All_NoRules_ReturnsAllowed**: an empty combination allows the connection.
- **ConnectionRules_All_ArrayChangedAfterCombining_KeepsOriginalRules**: replacing an element of the supplied array
  after combining does not change the result.
- **ConnectionRules_All_WithAndWithoutDefault_FanInFollowsTheDefault**: a combination that leaves out the default does
  not refuse a second wire into an occupied input, one that includes it does.

#### Denial Reasons (`Plexus-ConnectionRules-DenialReasons`)

- **ConnectionCheck_Allowed_HasNoReason**: an allowed result has a null reason.
- **ConnectionCheck_Denied_WithReason_KeepsReason**: the supplied reason text is kept.
- **ConnectionCheck_Denied_NullReason_GetsDefaultReason**: a null reason is replaced by a non-blank default.
- **DiagramPage_CanConnect_HostDenialWithoutReason_GetsDefaultReason**: a host rule that returns a bare denial still
  yields a reason to the caller.

#### Host Rule (`Plexus-ConnectionRules-HostRule`)

- **ConnectionRules_Default_AllowsAndHostRuleReceivesOutputThenInput**: ports asked in reversed order reach the rule as
  output then input, and a null rule is rejected.
- **ConnectionRules_SameType_RejectsMismatchedTypes**: a rule assigned by the host decides the query result.
- **DiagramPage_CanConnect_HostDenialWithoutReason_GetsDefaultReason**: the host decision is reported to the caller.

#### Degree Limits (`Plexus-ConnectionRules-DegreeLimits`, `ReconnectRulesTests.cs`)

- **ConnectionRules_MaxOutgoingWires_FullOutput_DeniesButMovingAWireOnTheSameOutputIsFine**: a third wire from an output
  with a limit of two is refused in either port order and names the output; moving an existing wire's target is allowed.
- **ConnectionRules_MaxOutgoingWires_ReconnectSourceOntoFullOutput_IsDenied**: moving a wire's source onto a full
  output is
  refused, onto a free output allowed.
- **ConnectionRules_MaxIncomingWires_FullInput_DeniesButTheMovingWireIsNotCounted**: a full input refuses another wire and
  a reconnect onto it, while a wire already on it may change its source.
- **ConnectionRules_MaxWires_LimitBelowOne_ThrowsArgumentOutOfRange**: limits below one are refused (error path).
