namespace AgentSmith.Server.Models;

/// <summary>
/// 2026-10-01-283db: an uploaded set after a check — the files to store, or why none of them is
/// stored. A refusal names the file and the limit; there is no partial outcome.
/// </summary>
public sealed record ReferenceSetCheck(IReadOnlyList<ReferenceUploadPart> Files, string? Refusal)
{
    public static ReferenceSetCheck Refused(string why) => new([], why);

    public bool IsRefused => Refusal is not null;
}
