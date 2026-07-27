using AgentSmith.Application.Services.Scope;
using AgentSmith.Contracts.Models.Configuration;
using FluentAssertions;

namespace AgentSmith.Tests.Scope;

// p0384: the SAME scope-classification call optionally names the subset of kept
// repos that must CHANGE (vs kept only for inspection). The evaluator validates
// it against the kept set; any unknown name drops the field (noted, not silent)
// so the keystone keeps anyCode semantics — the gate never enforces a
// requirement the classifier stated incoherently.
public sealed class RepoScopeExpectedChangesTests
{
    private static readonly IReadOnlyList<RepoConnection> Repos =
    [
        new RepoConnection { Name = "server" },
        new RepoConnection { Name = "client" },
        new RepoConnection { Name = "docs" },
    ];

    [Fact]
    public void RepoScopeParser_ExpectedChanges_ParsedAndValidatedAsSubset()
    {
        var reply = """
            {"repos": ["server", "client"], "expected_changes": ["server"],
             "confidence": 0.9, "rationale": "client kept for inspection"}
            """;

        var classification = RepoScopeParser.TryParse(reply);
        classification.Should().NotBeNull();
        classification!.ExpectedChanges.Should().BeEquivalentTo("server");

        var (scoped, record, expected) = RepoScopeEvaluator.Evaluate(classification, null, Repos);

        scoped.Should().NotBeNull();
        scoped!.Select(r => r.Name).Should().BeEquivalentTo("server", "client");
        expected.Should().BeEquivalentTo("server");
        record.Should().Contain("expected changes: [server]");
    }

    [Fact]
    public void RepoScopeParser_ExpectedChangesUnknownRepo_DegradesToNone()
    {
        var reply = """
            {"repos": ["server", "client"], "expected_changes": ["server", "unknown-repo"],
             "confidence": 0.9}
            """;

        var classification = RepoScopeParser.TryParse(reply);
        classification!.ExpectedChanges.Should().BeEquivalentTo("server", "unknown-repo");

        var (scoped, record, expected) = RepoScopeEvaluator.Evaluate(classification, null, Repos);

        // The repo narrowing itself stays intact — only expected_changes is dropped.
        scoped!.Select(r => r.Name).Should().BeEquivalentTo("server", "client");
        expected.Should().BeEmpty();
        record.Should().Contain("expected_changes dropped").And.Contain("unknown-repo");
    }

    [Fact]
    public void RepoScopeParser_ExpectedChangesAbsent_YieldsEmptySet()
    {
        var reply = """{"repos": ["server"], "confidence": 0.9}""";

        var classification = RepoScopeParser.TryParse(reply);
        classification!.ExpectedChanges.Should().BeNull();

        var (_, record, expected) = RepoScopeEvaluator.Evaluate(classification, null, Repos);

        expected.Should().BeEmpty();
        record.Should().NotContain("expected");
    }

    [Fact]
    public void RepoScopeEvaluator_LowConfidence_DropsExpectedChangesWithTheNarrowing()
    {
        var reply = """
            {"repos": ["server"], "expected_changes": ["server"], "confidence": 0.3}
            """;

        var (scoped, _, expected) = RepoScopeEvaluator.Evaluate(
            RepoScopeParser.TryParse(reply), null, Repos);

        scoped.Should().BeNull("low confidence keeps all repos");
        expected.Should().BeEmpty("an untrusted classification must not gate delivery");
    }
}
