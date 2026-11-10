using AgentSmith.Application.Services.SpecDialog;
using AgentSmith.Contracts.Models;
using FluentAssertions;

namespace AgentSmith.Tests.SpecDialog;

/// <summary>2026-11-10-14a3c: each distinct principles file once, as status, Project Specifics and path.</summary>
public sealed class DesignPrinciplesDigestTests
{
    private const string Body = "# Coding Principles\n\n<!-- agentsmith:principles composed=core+csharp status=proposed -->\n\n"
        + "## One responsibility per unit\n\nA long rule body.\n\n## Project Specifics (ratified additions)\n\n"
        + "- Money is decimal, never double.\n";

    [Fact]
    public void DesignPrinciplesDigest_TwoReposSameFile_RendersOnceWithStatusSpecificsAndPath()
    {
        var digest = DesignPrinciplesDigest.Render(
        [
            new ContextDocument("server", null, null, ".agentsmith/coding-principles.md", Body),
            new ContextDocument("worker", null, null, ".agentsmith/coding-principles.md", Body),
        ]);

        digest.Should().StartWith("## server, worker — `.agentsmith/coding-principles.md`")
            .And.Contain("Composed from core+csharp, status proposed.")
            .And.Contain("Money is decimal, never double.")
            .And.NotContain("A long rule body");
        digest.Split("## server").Length.Should().Be(2, "one file is shown once");
    }
}
