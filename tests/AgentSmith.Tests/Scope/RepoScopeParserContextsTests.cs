using AgentSmith.Application.Services.Scope;
using FluentAssertions;

namespace AgentSmith.Tests.Scope;

/// <summary>
/// p0336b: the classifier reply may carry an optional per-repo affected-context
/// map; absent or malformed contexts read as null (keep all), like a missing
/// repos array keeps all repos.
/// </summary>
public sealed class RepoScopeParserContextsTests
{
    [Fact]
    public void Parse_WithContexts_PopulatesPerRepoAffectedContexts()
    {
        var reply = """
            {"repos": ["server"], "contexts": {"server": ["api", "worker"]},
             "confidence": 0.9, "rationale": "MassTransit swap"}
            """;

        var result = RepoScopeParser.TryParse(reply);

        result.Should().NotBeNull();
        result!.Contexts.Should().NotBeNull();
        result.Contexts!["server"].Should().BeEquivalentTo("api", "worker");
    }

    [Fact]
    public void Parse_WithoutContexts_LeavesContextsNull()
    {
        var result = RepoScopeParser.TryParse("""{"repos": ["server"], "confidence": 0.9}""");

        result.Should().NotBeNull();
        result!.Contexts.Should().BeNull();
    }

    [Fact]
    public void Parse_ContextsNotAnObject_LeavesContextsNull()
    {
        var result = RepoScopeParser.TryParse("""{"repos": ["server"], "contexts": "nope", "confidence": 0.9}""");

        result!.Contexts.Should().BeNull();
    }
}
