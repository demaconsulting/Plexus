## ConnectionRules Unit Design

![Plexus Structure](PlexusView.svg)

### Purpose

ConnectionRules is the host-extensible policy layer that decides whether an output port may be wired to an input
port. It is a Software Unit directly under the Plexus system. It supplies the rule delegate type, ready-made rules that
hosts can use or combine, and the result type that carries a decision together with a human-readable reason. Rules run
after the structural checks that Plexus always enforces (one output and one input, same page, no self-loop, no
duplicate wire), so a rule only expresses domain policy. The default rule also carries the one policy Plexus ships
by default: fan-out (one output to many inputs) is allowed, but fan-in (several wires into one input) is not unless the
input's definition opts in with `PortDefinition.AllowsMultipleWires`. The same rules are used when a wire is created and
when an editor previews a drag, so a drop target can be refused with an explanation before the user releases the mouse.

### Data Model

- **ConnectionCheck**: a `readonly record struct` with `IsAllowed` and `Reason`. The private constructor stores the
  reason only for denials and replaces a null reason with the default text "The connection is not allowed.". The static
  `Allowed` property is the allowed result (null reason); `Denied(reason)` creates a denial. A `default` value is a
  denial with no reason, which callers must tolerate (see Error Handling).
- **ConnectionValidator**: a delegate `ConnectionCheck (Port from, Port to)`. `from` is always the output (source)
  port and `to` the input (target) port, for connects and for reconnects (the wire's new or kept ports), and this order
  is guaranteed.
- **ConnectionRules**: a static class that exposes the ready-made rules as static properties holding delegates:
  `Default`, `SameType`, `SingleInputWire`, the degree-limit factories `MaxOutgoingWires` and `MaxIncomingWires`, plus
  the `All` combinator. The rules hold no state. A private helper
  implements the single-input check shared by `Default` and `SingleInputWire`.

### Key Methods

- **ConnectionRules.Default**: allows the connection unless the target input already has a wire and its definition
  does not allow fan-in, in which case it denies with "Input 'Node.in' already has a wire; it does not accept more
  than one." Outputs are unrestricted. The wires of the input are counted with `Port.CountWiresIgnoringReconnect`, which
  leaves out the wire a `CanReconnect` check is moving (published by the page as the public `WireBeingReconnected`),
  so a reconnect
  onto the wire's own target, or a change of only its source, is not refused by itself. A diagram uses `Default`
  until the host assigns its own rule. Because the policy lives in the rule and not in the structural checks, a host
  that assigns a rule built without `Default` (or `SingleInputWire`) takes over the decision completely and may allow
  fan-in on every input; a host that wants domain policy and the default composes them with
  `All(ConnectionRules.Default, ...)`.
- **ConnectionRules.SameType**: allows the connection when the ordinal comparison of the two ports' type keys is
  equal; otherwise denies with a reason that names both keys.
- **ConnectionRules.MaxOutgoingWires(max)** and **ConnectionRules.MaxIncomingWires(max)**: factories (max at least 1,
  else `ArgumentOutOfRangeException`) of degree limits. They count the wires of the output (or input) with
  `Port.CountWiresIgnoringReconnect` and deny, with a reason naming the port and the limit, when the count has reached
  `max`; because the wire a reconnect check is moving is not counted, moving a wire along the same output or input and
  changing only one end is never refused by the limit itself. They do not include `Default`; hosts compose them with
  `All`.
- **ConnectionRules.SingleInputWire**: the same rule as `Default`, kept as a named building block: allows the
  connection when the target input port has no wires or allows several; otherwise denies with the reason above, which
  names the input port and its node.
- **ConnectionRules.All(rules)**: precondition is a non-null array without null elements, otherwise
  `ArgumentNullException`. It clones the array so later changes by the caller do not affect the result, then returns a
  validator that evaluates the rules in order and returns the first denial, or `Allowed` when all rules allow (including
  the case of no rules).
- **ConnectionCheck.Allowed** and **ConnectionCheck.Denied(reason)**: constructors of results, as described above.

### Error Handling

Rules express refusal as a result, never as an exception, so a refused connection is an ordinary outcome that carries
its reason. `All` validates its arguments eagerly with `ArgumentNullException`. A host rule can still return a
`default` result or a denial whose reason is null; `DiagramPage.CanConnect` in the Model subsystem detects a denial
without a reason and substitutes a default reason, so a denial reported to a caller always carries an explanation.
Exceptions thrown by a host rule are not caught here and propagate to the caller of the connection query.

### Dependencies

ConnectionRules uses the Model subsystem's `Port` type to read the type key, name, owning node and wire list of the
ports it is given. The ready-made rules depend on `Port.TypeKey`, `Port.AllowsMultipleWires`,
`Port.CountWiresIgnoringReconnect` and `Port.ToString` (`Node.port`). It
has no other dependencies. This is an intentional mutual reference with the Model: the validator contract receives
Model ports because it decides about them, while the Model owns the rule slot and calls the rule; a rule only reads the
ports it is given and never changes the model.

### Callers

The Model subsystem owns the rule slot: `Diagram.ConnectionRules` stores the active validator (initially `Default`) and
`DiagramPage.CanConnect` calls it, with the output port first and the input port second, after the structural checks; `Connect`
uses `CanConnect` and throws an `InvalidOperationException` with the reason when the connection is denied. The Avalonia
connect gesture calls `DiagramPage.CanConnect` to preview drop targets while the user drags a wire. Host applications assign
`Diagram.ConnectionRules`, typically with `ConnectionRules.All(ConnectionRules.Default, ...)`, and may call the
ready-made rules directly. `PreparedImport` (paste) asks `CanConnect` for every wire and skips refused ones with a
warning, while snapshot loading does not consult the rules, so a saved diagram is restored as written.
