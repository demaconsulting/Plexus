namespace DemaConsulting.Plexus;

/// <summary>
///     The outcome of asking whether two ports may be connected.
/// </summary>
/// <remarks>
///     A denial always carries a human-readable reason so editors can show why a drop target is
///     rejected (the Avalonia editor refuses the drop and shows the reason as feedback while a wire is dragged) and
///     <see cref="DiagramPage.Connect(Port, Port, IReadOnlyList{Point}?, string?)"/> can throw a meaningful exception.
///     Get a check from <see cref="DiagramPage.CanConnect"/> or <see cref="DiagramPage.CanReconnect"/>, or create one in a
///     rule with <see cref="Allowed"/> and <see cref="Denied"/>.
/// </remarks>
/// <example>
///     Ask before connecting and report the reason.
///     <code>
///     using DemaConsulting.Plexus;
///
///     var diagram = new Diagram(types);                    // types: the registry of the DemaConsulting.Plexus example
///     var page = diagram.Pages.Add("Main");
///     var a = page.AddNode("demo.Source", 0, 0, name: "A");
///     var b = page.AddNode("demo.Sink", 180, 0, name: "B");
///
///     ConnectionCheck check = page.CanConnect(a.Output("out"), b.Input("in"));
///     if (check.IsAllowed)
///     {
///         page.Connect("A.out", "B.in");
///     }
///     else
///     {
///         Console.WriteLine(check.Reason);                 // never null or empty when denied
///     }
///
///     ConnectionCheck ok = ConnectionCheck.Allowed;         // what a custom ConnectionValidator returns ...
///     ConnectionCheck no = ConnectionCheck.Denied("Not today.");   // ... or this, with a reason
///     </code>
/// </example>
public readonly record struct ConnectionCheck
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="ConnectionCheck"/> struct.
    /// </summary>
    /// <param name="isAllowed">Whether the connection is allowed.</param>
    /// <param name="reason">Why the connection is denied; ignored when allowed. Null or blank uses a default text.</param>
    private ConnectionCheck(bool isAllowed, string? reason)
    {
        // A denial always carries a readable reason so UIs never show an empty message
        IsAllowed = isAllowed;
        if (!isAllowed)
        {
            Reason = string.IsNullOrWhiteSpace(reason) ? "The connection is not allowed." : reason;
        }
    }

    /// <summary>
    ///     Gets a value indicating whether the connection is allowed.
    /// </summary>
    public bool IsAllowed { get; }

    /// <summary>
    ///     Gets the reason the connection was denied, or null when it is allowed.
    /// </summary>
    public string? Reason { get; }

    /// <summary>
    ///     Gets a result that allows the connection.
    /// </summary>
    public static ConnectionCheck Allowed => new(true, null);

    /// <summary>
    ///     Creates a result that denies the connection.
    /// </summary>
    /// <param name="reason">Why the connection is denied. Null or blank uses a default text.</param>
    /// <returns>The denial.</returns>
    public static ConnectionCheck Denied(string reason) => new(false, reason);
}
