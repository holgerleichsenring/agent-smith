using AgentSmith.Contracts.Memory;
using AgentSmith.Infrastructure.Persistence.Entities;
using AgentSmith.Infrastructure.Persistence.Repositories;
using AgentSmith.Tests.TestSupport;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace AgentSmith.Tests.Persistence;

/// <summary>
/// 2026-10-25-9324a: the memory ledger over the shipped migrations — read back by repository, a spec's outcome
/// kept once per run, spec and slug, a reliance replaced as a whole, and every row standing after its run is
/// deleted.
/// </summary>
public sealed class MemoryEvidenceStoreTests : IDisposable
{
    private const string Repo = "github.com/o/api";
    private static readonly DateTimeOffset T = new(2026, 10, 25, 9, 0, 0, TimeSpan.Zero);
    private readonly ServerStateStore _store = new();

    private MemoryEvidenceRepository Store => new(_store.ScopeFactory, new());

    private static MemoryEvidenceFact Fact(
        string slug, string kind, double weight = 0, string run = "run-1", string spec = "s-a", string repo = Repo) =>
        new(repo, slug, kind, weight, "sample", "A-1", run, spec, null, T);

    [Fact]
    public async Task MemoryEvidenceRepository_Append_ReadsBackByRepository()
    {
        var disputed = Fact("no-shims", MemoryEvidenceKinds.Contradicted, -2) with { Reason = "it led the spec wrong", EventSeq = 7 };
        await Store.AppendAsync(disputed, default);
        await Store.AppendAsync(Fact("no-shims", MemoryEvidenceKinds.Relied, repo: "github.com/o/web"), default);

        (await Store.OfRepositoryAsync(Repo, default)).Should().ContainSingle().Which.Should().Be(disputed);
        (await Store.OfRepositoryAsync("github.com/o/none", default)).Should().BeEmpty();
    }

    [Fact]
    public async Task MemoryEvidence_UpsertOutcome_ReplacesKindForSameRunSpecSlug()
    {
        await Store.UpsertOutcomeAsync(Fact("x", MemoryEvidenceKinds.Strained, -0.5), default);
        await Store.UpsertOutcomeAsync(Fact("x", MemoryEvidenceKinds.Confirmed, 1), default);
        await Store.UpsertOutcomeAsync(Fact("x", MemoryEvidenceKinds.Confirmed, 1, spec: "s-b"), default);
        await Store.AppendAsync(Fact("x", MemoryEvidenceKinds.Relied), default);

        (await Store.OfRepositoryAsync(Repo, default)).Select(f => (f.SpecId, f.Kind)).Should().BeEquivalentTo(new[]
        {
            ("s-a", MemoryEvidenceKinds.Confirmed), ("s-b", MemoryEvidenceKinds.Confirmed), ("s-a", MemoryEvidenceKinds.Relied),
        });
    }

    [Fact]
    public async Task MemoryEvidence_Replace_ReplacesOnlyThatKindOfThatRunSpec()
    {
        await Store.ReplaceAsync("run-1", "s-a", MemoryEvidenceKinds.Relied, [Fact("x", MemoryEvidenceKinds.Relied)], default);
        await Store.AppendAsync(Fact("x", MemoryEvidenceKinds.Contradicted, -2), default);
        await Store.AppendAsync(Fact("x", MemoryEvidenceKinds.Relied, spec: "s-b"), default);

        await Store.ReplaceAsync("run-1", "s-a", MemoryEvidenceKinds.Relied, [Fact("y", MemoryEvidenceKinds.Relied)], default);

        (await Store.OfRepositoryAsync(Repo, default)).Select(f => (f.Slug, f.SpecId, f.Kind)).Should().BeEquivalentTo(new[]
        {
            ("x", "s-a", MemoryEvidenceKinds.Contradicted), ("x", "s-b", MemoryEvidenceKinds.Relied), ("y", "s-a", MemoryEvidenceKinds.Relied),
        });
    }

    [Fact]
    public async Task RunDeletion_LeavesMemoryEvidence()
    {
        await using (var db = _store.Context())
        {
            db.Runs.Add(new Run { Id = "run-1", Project = "sample", Pipeline = "fix-bug", TicketId = "A-1" });
            await db.SaveChangesAsync();
        }
        await Store.UpsertOutcomeAsync(Fact("x", MemoryEvidenceKinds.Confirmed, 1), default);

        await using (var db = _store.Context()) await new RunDeletionRepository(db).DeleteAsync("run-1", default);

        await using (var db = _store.Context()) (await db.Runs.CountAsync()).Should().Be(0);
        (await Store.OfRepositoryAsync(Repo, default)).Should().ContainSingle()
            .Which.Should().Match<MemoryEvidenceFact>(f => f.Slug == "x" && f.Kind == MemoryEvidenceKinds.Confirmed && f.RunId == "run-1");
    }

    public void Dispose() => _store.Dispose();
}
