using System.Reflection;
using AgentSmith.Application.Services.Handlers;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Models;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AgentSmith.Tests.Handlers;

/// <summary>
/// Regression: init-project routes through SkillRound (p0130c) but has no
/// ticket — ticket-driven presets tell operators "run init-project first",
/// so init-project must work without a ticket. Pre-fix
/// <c>SkillRoundHandler.BuildDomainSection</c> hard-required the ticket
/// and threw <c>KeyNotFoundException: Key 'Ticket' not found</c>.
/// </summary>
public sealed class SkillRoundHandlerBootstrapTests
{
    [Fact]
    public void BuildDomainSection_NoTicketInContext_ReturnsSyntheticNoTicketBlock()
    {
        var sut = NewHandler();
        var pipeline = new PipelineContext();

        var section = InvokeBuildDomainSection(sut, pipeline);

        section.Should().Contain("## Ticket");
        section.Should().Contain("no ticket");
    }

    [Fact]
    public void BuildDomainSection_WithTicket_RendersTitleAndDescription()
    {
        var sut = NewHandler();
        var pipeline = new PipelineContext();
        pipeline.Set(ContextKeys.Ticket, new Ticket(
            id: new TicketId("AB-1"),
            title: "Fix the bug",
            description: "It's broken",
            acceptanceCriteria: null,
            status: "Open",
            source: "github"));

        var section = InvokeBuildDomainSection(sut, pipeline);

        section.Should().Contain("Fix the bug");
        section.Should().Contain("It's broken");
        section.Should().NotContain("no ticket");
    }

    private static SkillRoundHandler NewHandler() => new(
        Mock.Of<IChatClientFactory>(),
        Mock.Of<ISkillPromptBuilder>(),
        Mock.Of<IGateRetryCoordinator>(),
        Mock.Of<IUpstreamContextBuilder>(),
        new StructuredOutputInstructionBuilder(Mock.Of<IPromptCatalog>()),
        NullLogger<SkillRoundHandler>.Instance);

    private static string InvokeBuildDomainSection(SkillRoundHandler handler, PipelineContext pipeline)
    {
        var method = typeof(SkillRoundHandler).GetMethod(
            "BuildDomainSection",
            BindingFlags.Instance | BindingFlags.NonPublic)!;
        return (string)method.Invoke(handler, [pipeline])!;
    }
}
