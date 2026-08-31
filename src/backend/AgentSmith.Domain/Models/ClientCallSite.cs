namespace AgentSmith.Domain.Models;

/// <summary>
/// 2026-08-30-c6ec: one place in a first-party client's source where it calls the served
/// interface, as the reader of that source reported it.
/// <para>
/// The intent lives here rather than in the server: the served description says what MAY
/// be sent, and a call site says what IS. <paramref name="Operation"/> is the operation as
/// the client addresses it — an operation id, or a method and path.
/// </para>
/// </summary>
public sealed record ClientCallSite(
    string File,
    string Operation,
    IReadOnlyList<string> PropertiesSent,
    IReadOnlyList<string> PropertiesRead);
