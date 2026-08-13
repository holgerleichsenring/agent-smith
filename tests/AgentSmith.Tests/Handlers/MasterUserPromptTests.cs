using AgentSmith.Application.Services.Handlers;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Models;
using FluentAssertions;

namespace AgentSmith.Tests.Handlers;

// p0406: the master's user prompt left AgenticMasterHandler — the loop orchestrator was
// also rendering prompts. These pin what the prompt must still carry.
public sealed class MasterUserPromptTests
{
    private static readonly Repository Repo = new(new BranchName("run/1"), "https://example.invalid/repo.git");

    [Fact]
    public void Build_MultipleCheckouts_ListsEachAndExplainsThePrefix()
    {
        var prompt = MasterUserPrompt.Build(null, Repo, ["server", "worker"], string.Empty, string.Empty);

        prompt.Should().Contain("`server`").And.Contain("`worker`");
        prompt.Should().Contain("Address files with the repository prefix");
        prompt.Should().Contain("No ticket attached");
    }

    [Fact]
    public void Build_SingleCheckout_OmitsThePrefixInstruction()
    {
        var prompt = MasterUserPrompt.Build(null, Repo, ["server"], string.Empty, string.Empty);

        prompt.Should().Contain("`server`");
        prompt.Should().NotContain("Address files with the repository prefix");
    }

    [Fact]
    public void Build_NoCheckouts_FallsBackToTheRepositoryPath()
    {
        var prompt = MasterUserPrompt.Build(null, Repo, [], string.Empty, string.Empty);

        prompt.Should().Contain(Repo.LocalPath).And.Contain(Repo.CurrentBranch.ToString());
    }

    [Fact]
    public void Build_WithTicket_WrapsTheUntrustedFieldsAndKeepsTheSections()
    {
        var ticket = new Ticket(new TicketId("42"), "Title", "Description", "AC", "open", "azdo");

        var prompt = MasterUserPrompt.Build(ticket, Repo, ["server"], "conversation section", "attachments section");

        prompt.Should().Contain("**ID:** 42").And.Contain("**Acceptance Criteria:** AC");
        prompt.Should().Contain("conversation section").And.Contain("attachments section");
    }
}
