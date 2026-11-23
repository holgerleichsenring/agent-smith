namespace AgentSmith.Server.Services.Pocs.Import;

/// <summary>2026-11-23-933db: what an admitted upload answers — its id, the checks it passed (keys, display order), the
/// port, size and name proposal, and the comparison with the caller's PoC of that name, when there is one.</summary>
internal sealed record PocUploadAnswer(
    string UploadId, string FileName, string Target, int Port, int EntryCount, long UnpackedBytes, string ProposedName,
    IReadOnlyList<string> Checks, PocMatchView? Match);
