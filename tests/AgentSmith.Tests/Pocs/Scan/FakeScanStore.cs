using AgentSmith.Contracts.Pocs;

namespace AgentSmith.Tests.Pocs.Scan;

/// <summary>2026-11-23-933dc: one PoC and its revisions in memory, as the scan and commit steps read and write them —
/// the PoC state transitions of the database store included.</summary>
internal sealed class FakeScanStore : IPocScanStore, IPocIntakeStore
{
    internal string PocState { get; set; } = PocStates.Importing;

    internal Dictionary<int, Row> Rows { get; } = [];

    internal List<PocScanOutcome> Verdicts { get; } = [];

    internal sealed class Row
    {
        public string State { get; set; } = PocRevisionStates.Validated;
        public byte[]? Archive { get; set; }
        public string? ManifestJson { get; set; }
        public string? VerdictJson { get; set; }
        public string? Explanation { get; set; }
        public string? OverruledIdsJson { get; set; }
        public string? CommitSha { get; set; }
    }

    public Task<PocScanSubject?> ReadAsync(long pocId, int revision, CancellationToken ct) =>
        Task.FromResult(Rows.TryGetValue(revision, out var r)
            ? new PocScanSubject(pocId, "todo", PocState, "pocs/poc-ann/todo", revision, r.State, Rows.Keys.Max(), "s", revision.ToString(),
                r.ManifestJson, r.VerdictJson, r.OverruledIdsJson, r.CommitSha)
            : null);

    public Task AppendOverruledAsync(long pocId, int revision, string stage, IReadOnlyList<string> ids, CancellationToken ct)
    {
        Rows[revision].OverruledIdsJson = PocOverruledIds.FromJson(Rows[revision].OverruledIdsJson).Append(stage, ids).ToJson();
        return Task.CompletedTask;
    }

    public Task RecordVerdictAsync(PocScanOutcome outcome, CancellationToken ct)
    {
        Verdicts.Add(outcome);
        var row = Rows[outcome.Revision];
        (row.State, row.VerdictJson) = (outcome.State, outcome.VerdictJson);
        var blocked = outcome.State == PocRevisionStates.Blocked;
        if (blocked) row.Explanation = outcome.Explanation;
        if (blocked && PocState is PocStates.Importing or PocStates.Blocked) PocState = PocStates.Blocked;
        if (!blocked && PocState == PocStates.Blocked) PocState = PocStates.Importing;
        return Task.CompletedTask;
    }

    public Task RecordCommitAsync(long pocId, int revision, string commitSha, CancellationToken ct)
    {
        Rows[revision].CommitSha = commitSha;
        return Task.CompletedTask;
    }

    Task<PocIntakeSubject?> IPocIntakeStore.ReadAsync(long pocId, int revision, CancellationToken ct) => throw new NotSupportedException();

    public Task<byte[]?> ArchiveAsync(string uploadSession, string uploadId, CancellationToken ct) =>
        Task.FromResult(Rows.TryGetValue(int.Parse(uploadId), out var r) ? r.Archive : null);

    public Task RecordAsync(PocIntakeOutcome outcome, CancellationToken ct) => throw new NotSupportedException();
}
