using AgentSmith.Application.Services.SkillRounds.Strategies;
using AgentSmith.Contracts.Commands;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Models;
using FluentAssertions;

namespace AgentSmith.Tests.Handlers;

/// <summary>
/// Regression: init-project routes through SkillRound (p0130c) but has no
/// ticket — ticket-driven presets tell operators "run init-project first",
/// so init-project must work without a ticket. Pre-fix the ticket-domain
/// section throwed <c>KeyNotFoundException: Key 'Ticket' not found</c>.
/// p0147d: the domain-section logic lives on <see cref="DefaultSkillPromptStrategy"/>.
/// </summary>
public sealed class SkillRoundHandlerBootstrapTests
{
    private readonly DefaultSkillPromptStrategy _strategy = new();

    [Fact]
    public void BuildDomainSectionParts_NoTicketInContext_ReturnsSyntheticNoTicketBlock()
    {
        var pipeline = new PipelineContext();

        var (stable, _) = _strategy.BuildDomainSectionParts(pipeline);

        stable.Should().Contain("## Ticket");
        stable.Should().Contain("no ticket");
    }

    [Fact]
    public void BuildDomainSectionParts_WithTicket_RendersTitleAndDescription()
    {
        var pipeline = new PipelineContext();
        pipeline.Set(ContextKeys.Ticket, new Ticket(
            id: new TicketId("AB-1"),
            title: "Fix the bug",
            description: "It's broken",
            acceptanceCriteria: null,
            status: "Open",
            source: "github"));

        var (stable, _) = _strategy.BuildDomainSectionParts(pipeline);

        stable.Should().Contain("Fix the bug");
        stable.Should().Contain("It's broken");
        stable.Should().NotContain("no ticket");
    }
}
