using AgentSmith.Application.Services.Scope;
using AgentSmith.Contracts.Models.Configuration;
using FluentAssertions;

namespace AgentSmith.Tests.Scope;

// p0386: the scope decision is PER REPO — each verdict carries its own
// confidence, so a certain exclusion of one repo survives unrelated doubt about
// another (the live 2026-07-27 failure: one global 0.55 voided a confident
// exclusion and provisioned a never-used sandbox). Every doubtful per-repo path
// (no entry, low-confidence exclusion, unknown name) stays fail-open.
public sealed class RepoScopeVerdictTests
{
    private static readonly IReadOnlyList<RepoConnection> Repos =
    [
        new RepoConnection { Name = "server" },
        new RepoConnection { Name = "worker" },
        new RepoConnection { Name = "legacy-client" },
    ];

    [Fact]
    public void Evaluate_ConfidentExclusion_DropsRepo_DespiteOtherRepoDoubt()
    {
        // The pinned live shape: certain the legacy client is unrelated (0.9),
        // unsure whether the worker is affected (kept with doubt) — narrows to 2.
        var classification = new RepoScopeClassification(
            [
                new RepoScopeVerdict("server", Affected: true, Confidence: 0.9),
                new RepoScopeVerdict("worker", Affected: true, Confidence: 0.5,
                    Reason: "might share the messaging contracts"),
                new RepoScopeVerdict("legacy-client", Affected: false, Confidence: 0.9,
                    Reason: "frontend unrelated to the backend migration"),
            ], null);

        var (scoped, record, _) = RepoScopeEvaluator.Evaluate(classification, null, Repos);

        scoped.Should().NotBeNull("a confident exclusion must narrow despite doubt elsewhere");
        scoped!.Select(r => r.Name).Should().Equal("server", "worker");
        record.Should().Contain("legacy-client (confidence 0.90")
            .And.Contain("frontend unrelated to the backend migration");
    }

    [Fact]
    public void Evaluate_LowConfidenceExclusion_KeepsRepo()
    {
        var classification = new RepoScopeClassification(
            [
                new RepoScopeVerdict("server", Affected: true, Confidence: 0.9),
                new RepoScopeVerdict("worker", Affected: false, Confidence: 0.4),
                new RepoScopeVerdict("legacy-client", Affected: true, Confidence: 0.8),
            ], null);

        var (scoped, record, _) = RepoScopeEvaluator.Evaluate(classification, null, Repos);

        scoped.Should().BeNull("a below-floor exclusion is fail-open — the repo stays");
        record.Should().Contain("kept worker").And.Contain("below floor");
    }

    [Fact]
    public void Evaluate_RepoWithoutVerdict_Kept()
    {
        var classification = new RepoScopeClassification(
            [
                new RepoScopeVerdict("server", Affected: true, Confidence: 0.9),
                new RepoScopeVerdict("legacy-client", Affected: false, Confidence: 0.9),
            ], null);

        var (scoped, _, _) = RepoScopeEvaluator.Evaluate(classification, null, Repos);

        scoped!.Select(r => r.Name).Should().Equal("server", "worker");
    }

    [Fact]
    public void Evaluate_UnknownRepoNameEntry_IgnoredAndNoted()
    {
        // An unknown-named entry never triggers keep-all — the valid exclusion
        // still narrows, and the ignored name lands on the record.
        var classification = new RepoScopeClassification(
            [
                new RepoScopeVerdict("server", Affected: true, Confidence: 0.9),
                new RepoScopeVerdict("worker", Affected: true, Confidence: 0.9),
                new RepoScopeVerdict("legacy-client", Affected: false, Confidence: 0.9),
                new RepoScopeVerdict("ghost-repo", Affected: false, Confidence: 0.9),
            ], null);

        var (scoped, record, _) = RepoScopeEvaluator.Evaluate(classification, null, Repos);

        scoped!.Select(r => r.Name).Should().Equal("server", "worker");
        record.Should().Contain("ignored unknown repo").And.Contain("ghost-repo");
    }

    [Fact]
    public void Evaluate_EveryRepoConfidentlyExcluded_KeepsAll()
    {
        // A run cannot proceed with zero repos — total exclusion is a fallback.
        var classification = new RepoScopeClassification(
            Repos.Select(r => new RepoScopeVerdict(r.Name!, Affected: false, Confidence: 0.9))
                .ToList(), null);

        var (scoped, record, _) = RepoScopeEvaluator.Evaluate(classification, null, Repos);

        scoped.Should().BeNull();
        record.Should().Contain("fallback").And.Contain("excluded every repo");
    }

    [Fact]
    public void TryParse_ObjectEntries_And_BareStringEntries_Parse()
    {
        // Object entries are the contract; a bare string is tolerated as
        // affected=true (LLM output fuzz — a kept repo is always fail-open).
        var reply = """
            {"repos": [{"name": "server", "affected": true, "confidence": 0.85, "reason": "endpoint lives here"},
                       {"name": "legacy-client", "affected": false, "confidence": 0.9},
                       "worker"],
             "rationale": "server bug"}
            """;

        var classification = RepoScopeParser.TryParse(reply);

        classification.Should().NotBeNull();
        classification!.Repos.Should().HaveCount(3);
        classification.Repos[0].Should().Be(
            new RepoScopeVerdict("server", Affected: true, Confidence: 0.85, "endpoint lives here"));
        classification.Repos[1].Should().Be(
            new RepoScopeVerdict("legacy-client", Affected: false, Confidence: 0.9));
        classification.Repos[2].Should().Be(
            new RepoScopeVerdict("worker", Affected: true, Confidence: 0));
    }

    [Fact]
    public void Evaluate_ExpectedChanges_SemanticsUnchanged()
    {
        // p0384 semantics ride along untouched: validated subset of the KEPT
        // repos, recorded on the line; the narrowing itself is per-repo now.
        var classification = new RepoScopeClassification(
            [
                new RepoScopeVerdict("server", Affected: true, Confidence: 0.9),
                new RepoScopeVerdict("worker", Affected: true, Confidence: 0.6),
                new RepoScopeVerdict("legacy-client", Affected: false, Confidence: 0.9),
            ], null, ExpectedChanges: ["server"]);

        var (scoped, record, expected) = RepoScopeEvaluator.Evaluate(classification, null, Repos);

        scoped!.Select(r => r.Name).Should().Equal("server", "worker");
        expected.Should().BeEquivalentTo("server");
        record.Should().Contain("expected changes: [server]");
    }
}
