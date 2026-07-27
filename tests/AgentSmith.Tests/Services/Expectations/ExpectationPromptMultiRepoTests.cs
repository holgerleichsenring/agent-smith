using AgentSmith.Application.Services.Expectations;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Models;
using FluentAssertions;

namespace AgentSmith.Tests.Services.Expectations;

// p0384: the drafting prompt enumerates the scoped repo set + per-repo code
// maps so acceptance criteria are drafted per repo, never against an unnamed
// single codebase. Shape test — repo enumeration, not exact wording.
public sealed class ExpectationPromptMultiRepoTests
{
    private static readonly Ticket Ticket = new(
        new TicketId("42"), "Add tracing", "Trace across services", null, "Open", "AzureDevOps");

    [Fact]
    public void ExpectationPrompt_MultiRepo_EnumeratesScopedRepos()
    {
        var pipeline = new PipelineContext();
        pipeline.Set<IReadOnlyList<RepoConnection>>(ContextKeys.Repos,
        [
            new RepoConnection { Name = "server" },
            new RepoConnection { Name = "client" },
        ]);
        pipeline.Set<IReadOnlyDictionary<string, string>>(
            ContextKeys.RepoCodeMaps,
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["server"] = "primary_language: csharp",
                ["client"] = "primary_language: typescript",
            });

        var prompt = ExpectationPromptComposer.ComposeUserPrompt(Ticket, pipeline);

        prompt.Should().Contain("server").And.Contain("client");
        prompt.Should().Contain("Repositories in scope");
        prompt.Should().Contain("### Repository: server").And.Contain("### Repository: client");
        prompt.Should().Contain("primary_language: csharp").And.Contain("primary_language: typescript");
    }

    [Fact]
    public void ExpectationPrompt_NoRepoContext_StillRendersTicket()
    {
        var prompt = ExpectationPromptComposer.ComposeUserPrompt(Ticket, new PipelineContext());

        prompt.Should().Contain("Add tracing");
        prompt.Should().NotContain("Repositories in scope");
    }
}
