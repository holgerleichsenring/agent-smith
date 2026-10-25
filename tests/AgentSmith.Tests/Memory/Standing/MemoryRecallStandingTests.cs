using AgentSmith.Application.Services.Memory;
using AgentSmith.Application.Services.Tools;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Memory;
using AgentSmith.Tests.TestHelpers;
using FluentAssertions;

namespace AgentSmith.Tests.Memory.Standing;

/// <summary>
/// 2026-10-25-6c1f: recall heads each match with the standing and record the index shows, and a disputed match
/// leads with a marker that says not to act on it while keeping the body that shows why.
/// </summary>
public sealed class MemoryRecallStandingTests
{
    private const string Root = "/work";
    private readonly InMemorySandboxFileReader _reader = new();

    private static MemoryEvidenceFact Fact(string slug, string kind, double weight) =>
        new("api", slug, kind, weight, "sample", "A-1", "run-1", "s-a", null, DateTimeOffset.UtcNow);

    private async Task<MemoryRecallToolHost> HostAsync(params MemoryEvidenceFact[] facts)
    {
        var store = new MemoryStore(_reader, Root);
        await store.UpsertAsync(new MemoryEntry("retry-caps", "Retries are capped at three", MemoryEntryType.Feedback,
            "Cap retries at three; the gateway drops a fourth."), CancellationToken.None);
        await store.UpsertAsync(new MemoryEntry("retry-backoff", "Retries back off exponentially", MemoryEntryType.Project,
            "Back off 2s, 4s, 8s between retries."), CancellationToken.None);
        return new MemoryRecallToolHost(TestRepositories.One(_reader, "api"), Root,
            MemoryStandings.Reader(new Ledger(facts)), new PipelineContext());
    }

    [Fact]
    public async Task Recall_ShowsStandingAndRecordPerMatch()
    {
        var host = await HostAsync(Fact("retry-backoff", MemoryEvidenceKinds.Confirmed, MemoryStandingWeights.Confirmed));

        var result = await host.Recall("retries");

        result.Should().MatchRegex(@"## retry-backoff \(project, holding · confirmed 1 · strained 0 · contradicted 0 · last \d{4}-\d{2}-\d{2}\)")
            .And.Contain("## retry-caps (feedback, unconfirmed · no record yet)")
            .And.NotContain(MemoryRecallToolHost.DisputedMarker);
    }

    [Fact]
    public async Task Recall_DisputedMatch_LeadsWithMarkerAndKeepsBody()
    {
        var host = await HostAsync(Fact("retry-caps", MemoryEvidenceKinds.Contradicted, MemoryStandingWeights.Disputed));

        var result = await host.Recall("type:feedback retries");

        var lines = result.Split('\n');
        lines[0].Should().StartWith("## retry-caps (feedback, disputed · confirmed 0 · strained 0 · contradicted 1");
        lines[1].Should().Be(MemoryRecallToolHost.DisputedMarker);
        MemoryRecallToolHost.DisputedMarker.Should().StartWith("DISPUTED").And.Contain("do not act on it").And.NotContain("—");
        result.Should().Contain("Cap retries at three; the gateway drops a fourth.");
    }

    [Fact]
    public async Task Recall_NoLedger_ReadsUnconfirmed()
    {
        await new MemoryStore(_reader, Root).UpsertAsync(new MemoryEntry("old", "Written before 9324e", MemoryEntryType.Feedback,
            "Old body.", "proposed"), CancellationToken.None);
        var host = new MemoryRecallToolHost(TestRepositories.One(_reader), Root, MemoryStandings.Reader(), new PipelineContext());

        var result = await host.Recall("[[old]]");

        result.Should().Contain("## old (feedback, unconfirmed · no record yet)").And.NotContain("proposed");
    }

    private sealed class Ledger(IReadOnlyList<MemoryEvidenceFact> facts) : IMemoryEvidenceStore
    {
        public Task ReplaceAsync(string runId, string specId, string kind, IReadOnlyList<MemoryEvidenceFact> f, CancellationToken ct) => Task.CompletedTask;
        public Task UpsertOutcomeAsync(MemoryEvidenceFact fact, CancellationToken ct) => Task.CompletedTask;
        public Task AppendAsync(MemoryEvidenceFact fact, CancellationToken ct) => Task.CompletedTask;
        public Task<IReadOnlyList<MemoryEvidenceFact>> OfRepositoryAsync(string repoKey, CancellationToken ct) => Task.FromResult(facts);
    }
}
