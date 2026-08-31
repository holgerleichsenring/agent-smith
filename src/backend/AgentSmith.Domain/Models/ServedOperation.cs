namespace AgentSmith.Domain.Models;

/// <summary>
/// 2026-08-30-c6ec: one operation of the interface a run holds a served description of,
/// reduced to what a client can be compared against — the operation's identity and the
/// property names it ACCEPTS and RETURNS.
/// <para>
/// <paramref name="AcceptedProperties"/> is what a caller may send (parameters and request
/// body fields); <paramref name="ReturnedProperties"/> is what a caller may read from a
/// success response. Both are names only: the difference this phase computes is over
/// names, and a type or a description would only make two descriptions of the same thing.
/// </para>
/// </summary>
public sealed record ServedOperation(
    string Method,
    string Path,
    string? OperationId,
    IReadOnlyList<string> AcceptedProperties,
    IReadOnlyList<string> ReturnedProperties)
{
    /// <summary>How the operation is named where an operation id is absent.</summary>
    public string Signature => $"{Method} {Path}";
}
