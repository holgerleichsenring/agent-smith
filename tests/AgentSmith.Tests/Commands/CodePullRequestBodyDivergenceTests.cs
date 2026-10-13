using AgentSmith.Application.Models;
using AgentSmith.Application.Services.Handlers;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Models.Structure;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Models;
using FluentAssertions;

namespace AgentSmith.Tests.Commands;

/// <summary>2026-10-12-fbbda: a code PR body lists the divergences of its own repository.</summary>
public sealed class CodePullRequestBodyDivergenceTests
{
    private static CommitAndPRContext Context()
    {
        var pipeline = new PipelineContext();
        pipeline.Set<IReadOnlyList<StructureFinding>>(ContextKeys.StructureDivergences,
        [
            new("src/A.cs", "A.Run()", StructureMetric.FunctionLines, 24, 20, StructureSeverity.Divergence, "server"),
            new("web/b.ts", "b()", StructureMetric.FunctionLines, 23, 20, StructureSeverity.Divergence, "client"),
        ]);
        var ticket = new Ticket(new TicketId("1"), "T", "Description", null, "Open", "GitHub");
        return new CommitAndPRContext(new Repository(new BranchName("fix/1"), "u"), [], ticket,
            [new RepoConnection { Name = "server" }, new RepoConnection { Name = "client" }], new TrackerConnection(), pipeline);
    }

    [Fact]
    public void CodePullRequestBody_Divergences_ListedForTheirRepositoryOnly()
    {
        var body = CodePullRequestBody.Compose(Context(), string.Empty, null, null, "server");

        body.Should().Contain(StructureDivergenceSection.Heading)
            .And.Contain("- within tolerance: src/A.cs A.Run() function_lines 24/20")
            .And.NotContain("web/b.ts");
    }

    [Fact]
    public void CodePullRequestBody_NoRepository_ListsNoDivergence()
    {
        CodePullRequestBody.Compose(Context(), string.Empty, null, null)
            .Should().NotContain(StructureDivergenceSection.Heading, "the spec-only body names no repository");
    }

    [Fact]
    public void CodePullRequestBody_RepositoryWithoutDivergence_HasNoSection()
    {
        CodePullRequestBody.Compose(Context(), string.Empty, null, null, "docs")
            .Should().NotContain(StructureDivergenceSection.Heading);
    }
}
