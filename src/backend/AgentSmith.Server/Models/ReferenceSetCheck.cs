namespace AgentSmith.Server.Models;

/// <summary>
/// 2026-10-01-283db: an uploaded set after a check — the files to store, or why none of them is
/// stored. A refusal names the file and the limit.
/// 2026-10-02-075da: and the entries left out of it with their reasons — a rebuildable folder, a
/// file over the per-file bound. Leaving one out is not a partial outcome: it was never a
/// candidate for the set.
/// </summary>
public sealed record ReferenceSetCheck(
    IReadOnlyList<ReferenceUploadPart> Files, string? Refusal, IReadOnlyList<ReferenceLeftOut>? LeftOutEntries = null)
{
    public static ReferenceSetCheck Refused(string why) => new([], why);

    public bool IsRefused => Refusal is not null;

    public IReadOnlyList<ReferenceLeftOut> LeftOut => LeftOutEntries ?? [];
}
