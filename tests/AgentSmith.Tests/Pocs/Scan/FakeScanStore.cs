using AgentSmith.Contracts.Pocs;

namespace AgentSmith.Tests.Pocs.Scan;

/// <summary>2026-11-23-933dc: one PoC and its revisions in memory, as the scan and commit steps read and write them —
/// the PoC state transitions of the database store included.</summary>
internal sealed class FakeScanStore : IPocScanStore, IPocIntakeStore, IPocBuildStore
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
        public string? Digest { get; set; } // 2026-11-23-933dd
        public string? PipelineRunId { get; set; }
        public string? PipelineUrl { get; set; }
        public string? ImageVerdictJson { get; set; }
        public string? BlockedStage { get; set; }
    }

    /// <summary>2026-11-23-933dd: every pipeline run id stored, in order (a clear stores null).</summary>
    internal List<string?> StoredRuns { get; } = [];

    public Task<PocScanSubject?> ReadAsync(long pocId, int revision, CancellationToken ct) =>
        Task.FromResult(Rows.TryGetValue(revision, out var r)
            ? new PocScanSubject(pocId, "todo", PocState, "pocs/poc-ann/todo", revision, r.State, Rows.Keys.Max(), "s", revision.ToString(),
                r.ManifestJson, r.VerdictJson, r.OverruledIdsJson, r.CommitSha)
            {
                Target = "main", BlockedStage = r.BlockedStage, ImageVerdictJson = r.ImageVerdictJson, Digest = r.Digest,
                PipelineRunId = r.PipelineRunId, PipelineUrl = r.PipelineUrl,
            }
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
        var blocked = outcome.State == PocRevisionStates.Blocked;
        (row.State, row.VerdictJson, row.BlockedStage) = (outcome.State, outcome.VerdictJson, blocked ? PocScanStages.Code : null);
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

    public Task RecordPipelineRunAsync(long pocId, int revision, string? runId, string? url, CancellationToken ct)
    {
        (Rows[revision].PipelineRunId, Rows[revision].PipelineUrl) = (runId, url);
        StoredRuns.Add(runId);
        return Task.CompletedTask;
    }

    public Task RecordImageBlockAsync(long pocId, int revision, string verdictJson, CancellationToken ct)
    {
        var row = Rows[revision];
        (row.State, row.ImageVerdictJson, row.BlockedStage) = (PocRevisionStates.Blocked, verdictJson, PocScanStages.Image);
        if (PocState is PocStates.Importing or PocStates.Blocked) PocState = PocStates.Blocked;
        return Task.CompletedTask;
    }

    public Task RecordBuiltAsync(long pocId, int revision, string digest, string verdictJson, CancellationToken ct)
    {
        var row = Rows[revision];
        (row.State, row.Digest, row.ImageVerdictJson, row.BlockedStage) = (PocRevisionStates.Built, digest, verdictJson, null);
        if (PocState == PocStates.Blocked) PocState = PocStates.Importing;
        return Task.CompletedTask;
    }

    Task<PocIntakeSubject?> IPocIntakeStore.ReadAsync(long pocId, int revision, CancellationToken ct) => throw new NotSupportedException();

    public Task<byte[]?> ArchiveAsync(string uploadSession, string uploadId, CancellationToken ct) =>
        Task.FromResult(Rows.TryGetValue(int.Parse(uploadId), out var r) ? r.Archive : null);

    public Task RecordAsync(PocIntakeOutcome outcome, CancellationToken ct) => throw new NotSupportedException();
}
