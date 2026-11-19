using AgentSmith.Application.Services.Scope;
using FluentAssertions;

namespace AgentSmith.Tests.Scope;

// p0386: the scope reply's per-repo verdicts parse. 2026-11-18-9c1cb: nothing narrows on them any more.
public sealed class RepoScopeVerdictTests
{
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
}
