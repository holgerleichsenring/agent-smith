using AgentSmith.Contracts.Pocs;
using AgentSmith.Tests.Pocs.GitOps;

namespace AgentSmith.Tests.Pocs.Acts;

/// <summary>2026-11-23-c81ed: the act's view of the in-memory row the GitOps commit writes, with a state and the revisions'
/// digests and rollout states.</summary>
internal sealed class FakeActStore(FakeGitOpsStore rows) : IPocActStore
{
    public string State { get; set; } = PocStates.Ready;

    public Dictionary<int, PocActRevision> Revisions { get; } = [];

    public Task<PocActSubject?> ReadAsync(long pocId, CancellationToken ct)
    {
        var p = rows.Poc;
        return Task.FromResult<PocActSubject?>(pocId != p.PocId ? null
            : new PocActSubject(p.PocId, p.Name, p.Target, State, p.Replicas, p.Revision, p.ImageDigest, p.ReadinessPath));
    }

    public Task<PocActRevision?> RevisionAsync(long pocId, int revision, CancellationToken ct) =>
        Task.FromResult(Revisions.GetValueOrDefault(revision));
}
