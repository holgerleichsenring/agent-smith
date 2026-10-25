using AgentSmith.Application.Services.Memory;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Memory;
using AgentSmith.Domain.Entities;
using AgentSmith.Tests.TestHelpers;
using FluentAssertions;

namespace AgentSmith.Tests.Memory.Standing;

/// <summary>
/// 2026-10-25-9324d: a memory's standing comes from its evidence — fading with a 90-day half-life, disputed only
/// by a contradiction that leaves the sum negative — and the master's index shows it per line, holding first,
/// disputed entries left out and counted; MEMORY.md's labels follow at record time.
/// </summary>
public sealed class MemoryStandingTests
{
    private const string Repo = "github.com/o/api";
    private static readonly DateTimeOffset Now = new(2026, 10, 25, 12, 0, 0, TimeSpan.Zero);
    private readonly MemoryStandingCalculator _calculator = new();

    private static MemoryEvidenceFact Fact(string slug, string kind, double weight, int daysAgo = 0) =>
        new(Repo, slug, kind, weight, "sample", "A-1", "run-1", "s-a", null, Now.AddDays(-daysAgo));

    private static MemoryEvidenceFact Confirmed(string slug, int daysAgo = 0) =>
        Fact(slug, MemoryEvidenceKinds.Confirmed, MemoryStandingWeights.Confirmed, daysAgo);

    private static MemoryEvidenceFact Strained(string slug, int daysAgo = 0) =>
        Fact(slug, MemoryEvidenceKinds.Strained, MemoryStandingWeights.Strained, daysAgo);

    private const string Index = "# Memory index\n\n"
        + "- [y](y.md) (project) — two strains\n"
        + "- [x](x.md) (feedback, proposed) — led one spec wrong\n"
        + "- [z](z.md) (project) — confirmed twice\n"
        + "- [w](w.md) (reference) — never used\n";

    [Fact]
    public void MemoryStanding_TwoConfirmedOneStrained_Holding()
    {
        var standing = _calculator.Of([Confirmed("z", 5), Confirmed("z", 2), Strained("z"),
            Fact("z", MemoryEvidenceKinds.Relied, 0)], Now)["z"];

        (standing.Label, standing.Confirmed, standing.Strained, standing.Contradicted).Should()
            .Be((MemoryStandingLabels.Holding, 2, 1, 0));
        standing.Record.Should().Be("confirmed 2 · strained 1 · contradicted 0 · last 2026-10-25");
    }

    [Fact]
    public void MemoryStanding_Contradicted_DisputedAndLeftOut()
    {
        var standings = _calculator.Of([
            Confirmed("x"), Fact("x", MemoryEvidenceKinds.Contradicted, MemoryStandingWeights.JudgedNotMet),
            Fact("x", MemoryEvidenceKinds.Contradicted, MemoryStandingWeights.Disputed),
            Strained("y"), Strained("y"), Confirmed("z"), Confirmed("z"),
        ], Now);

        standings["x"].Label.Should().Be(MemoryStandingLabels.Disputed);
        standings["y"].Label.Should().Be(MemoryStandingLabels.Unconfirmed, "strains alone never dispute");
        var shown = new MemoryIndexComposer().Compose(Index, standings);
        shown.Should().NotContain("[x](x.md)").And.EndWith("1 disputed memory is left out: the runs' evidence turned against it.");
        shown.Should().Contain("- [y](y.md) (project, unconfirmed · confirmed 0 · strained 2 · contradicted 0 · last 2026-10-25) — two strains");
    }

    [Fact]
    public void MemoryStanding_OldEvidence_WeighsLess()
    {
        var standings = _calculator.Of([Confirmed("a", daysAgo: 180), Strained("a"), Confirmed("b"), Strained("b")], Now);

        standings["a"].Sum.Should().BeApproximately(0.25 - 0.5, 1e-9, "two half-lives leave a quarter of the confirmation");
        standings["a"].Label.Should().Be(MemoryStandingLabels.Unconfirmed);
        standings["b"].Label.Should().Be(MemoryStandingLabels.Holding);
    }

    [Fact]
    public void MemoryIndex_ShowsRecordPerLine_HoldingFirst()
    {
        var shown = new MemoryIndexComposer().Compose(Index, _calculator.Of([Confirmed("z"), Confirmed("z", 3)], Now));

        shown.Split('\n').Should().Equal(
            "# Memory index",
            "- [z](z.md) (project, holding · confirmed 2 · strained 0 · contradicted 0 · last 2026-10-25) — confirmed twice",
            "- [y](y.md) (project, unconfirmed · no record yet) — two strains",
            "- [x](x.md) (feedback, unconfirmed · no record yet) — led one spec wrong",
            "- [w](w.md) (reference, unconfirmed · no record yet) — never used");
    }

    [Fact]
    public async Task MemoryIndexLabelWriter_RewritesTheLabelsFromTheLedger()
    {
        var store = new FakeLedger([Confirmed("z"), Strained("y")]);
        var files = new InMemorySandboxFileReader();
        var path = $"{Repository.SandboxWorkPath}/.agentsmith/memory/MEMORY.md";
        files.Files[path] = Index;

        var staged = await MemoryStandings.LabelWriter(store).WriteAsync(new PipelineContext(), files, "api", default);

        staged.Should().Be(".agentsmith/memory/MEMORY.md");
        files.Files[path].Should().Contain("- [z](z.md) (project, holding) — confirmed twice")
            .And.Contain("- [x](x.md) (feedback, unconfirmed) — led one spec wrong").And.StartWith("# Memory index\n\n");
        (await MemoryStandings.LabelWriter(store).WriteAsync(new PipelineContext(), files, "api", default))
            .Should().BeNull("nothing moved");
    }

    [Fact]
    public void MemoryIndexUpdater_UpdatedEntry_KeepsTheLabelTheEvidenceGave()
    {
        var updated = MemoryIndexUpdater.Upsert("# Memory index\n\n- [z](z.md) (project, holding) — old\n",
            new MemoryEntry("z", "new words", MemoryEntryType.Project, "body", "proposed"));

        updated.Should().Contain("- [z](z.md) (project, holding) — new words").And.NotContain("proposed");
        MemoryIndexUpdater.FormatLine(new MemoryEntry("q", "d", MemoryEntryType.Feedback, "b", "proposed"))
            .Should().Be("- [q](q.md) (feedback) — d");
    }

    private sealed class FakeLedger(IReadOnlyList<MemoryEvidenceFact> facts) : IMemoryEvidenceStore
    {
        public Task ReplaceAsync(string runId, string specId, string kind, IReadOnlyList<MemoryEvidenceFact> f, CancellationToken ct) => Task.CompletedTask;
        public Task UpsertOutcomeAsync(MemoryEvidenceFact fact, CancellationToken ct) => Task.CompletedTask;
        public Task AppendAsync(MemoryEvidenceFact fact, CancellationToken ct) => Task.CompletedTask;
        public Task<IReadOnlyList<MemoryEvidenceFact>> OfRepositoryAsync(string repoKey, CancellationToken ct) =>
            Task.FromResult(repoKey == "api" ? facts : (IReadOnlyList<MemoryEvidenceFact>)[]);
    }
}
