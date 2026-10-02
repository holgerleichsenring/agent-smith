namespace AgentSmith.Server.Models;

/// <summary>
/// 2026-10-02-0d72: what an upload answers — the stored set as the transcript shows it.
/// 2026-10-02-075da: and the entries left out of it with their reasons (the first
/// <see cref="Services.References.ReferenceUploadLimits.MaxLeftOutListed"/> and how many in all),
/// and the stored files that commonly hold credentials, which the model and a run will read.
/// </summary>
public sealed record ReferenceUploadView(
    string SetId, string Name, int Files, long Bytes, DateTimeOffset At,
    IReadOnlyList<ReferenceLeftOut> LeftOut, int LeftOutCount, IReadOnlyList<string> CredentialFiles);
