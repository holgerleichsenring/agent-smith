using AgentSmith.Application.Services.Tools;
using AgentSmith.Contracts.Dialogue;
using AgentSmith.Domain.Models;
using AgentSmith.Infrastructure.Services.Providers.Tickets;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.TeamFoundation.WorkItemTracking.WebApi.Models;

namespace AgentSmith.Tests.Providers.Tickets;

/// <summary>
/// 2026-09-28-1da5c: what a tracker shows against a ticket, and what it says when it cannot.
/// <para>
/// A design conversation asked whether a ticket's branches were finished could only answer that
/// every link demanded a sign-in — its one reach outward carries no credential. What it lacked was
/// not permission but a QUESTION it could ask through a port that already holds one.
/// </para>
/// </summary>
public sealed class TicketLinkedWorkTests
{
    [Fact]
    public void TicketWork_AzureDevOps_ReadsTheTwoArtifactShapesItDefines()
    {
        AzureDevOpsLinkedWork.Linked(Relation(
            "vstfs:///Git/PullRequestId/7b2%2F1a4%2F9335"))
            // "!9335" is Azure DevOps' own way of referring to a pull request.
            .Should().BeEquivalentTo(new { Kind = "pull request", Name = "!9335" });
        AzureDevOpsLinkedWork.Linked(Relation(
            "vstfs:///Git/Ref/7b2%2F1a4%2FGBagent-smith%2F19400"))
            // The branch name carries its own encoded slashes, and Azure DevOps prefixes it GB.
            .Should().BeEquivalentTo(new { Kind = "branch", Name = "agent-smith/19400" });
    }

    [Fact]
    public void TicketWork_ARelationThatIsNotLinkedWork_IsNotReported()
    {
        // A work item's parents, children and attachments are relations too, and are not what
        // "what is being worked on this ticket" asked.
        AzureDevOpsLinkedWork.Linked(Relation("vstfs:///WorkItemTracking/WorkItem/412"))
            .Should().BeNull();
        AzureDevOpsLinkedWork.Linked(Relation("https://dev.azure.com/o/p/_apis/wit/attachments/1"))
            .Should().BeNull();
    }

    [Fact]
    public async Task TicketWork_Jira_RefusesAndNamesTheIntegrationItWouldNeed()
    {
        var answer = await new JiraLinkedWork().ForAsync(new TicketId("DPG-1"), default);

        answer.Answered.Should().BeFalse("a refusal is not a ticket with no work against it");
        answer.Reason.Should().Contain("development-tools");
        answer.Work.Should().BeEmpty();
    }

    [Fact]
    public async Task TicketWork_ATicketWithNoLinkedWork_IsNotARefusal()
    {
        var answer = TicketLinkedWorkResult.None;

        answer.Answered.Should().BeTrue();
        answer.Work.Should().BeEmpty();
        // The two cases the design has to keep apart, in one assertion.
        TicketLinkedWorkResult.Refused("unreachable").Answered.Should().BeFalse();
        await Task.CompletedTask;
    }

    [Fact]
    public void TicketWorkTool_TheToolItOffers_TakesNoTicketArgument()
    {
        var tool = new TicketWorkToolHost(new Bound()).GetTools(null, null).Single();

        tool.Name.Should().Be("ticket_work");
        // The ticket is the one the turn was seeded with, as the withdrawal's session id is.
        tool.JsonSchema.ToString().Should().NotContain("ticket");
    }

    private static WorkItemRelation Relation(string url) => new() { Url = url };

    private sealed class Bound : IBoundTicketWork
    {
        public Task<TicketLinkedWorkResult> ForAsync(CancellationToken cancellationToken) =>
            Task.FromResult(TicketLinkedWorkResult.None);
    }
}
