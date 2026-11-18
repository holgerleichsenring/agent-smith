using AgentSmith.Application.Services.SpecDialog;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;
using AgentSmith.Server.Services;
using AgentSmith.Server.Services.SpecDialog;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentSmith.Tests.SpecDialog.ProposalReview;

/// <summary>2026-10-14-dbcek: the existence port resolves from the server's own composition. 2026-11-18-9c1ca: a ticket
/// list gets the mechanical checks only — ticket existence, read on the tracker, no model call.</summary>
[Trait("TestProcess", "env-1")]
[Collection(TestSupport.EnvVarCollection.Name)]
public sealed class TicketPlanCompositionTests
{
    [Fact]
    public void TicketExistence_ServerComposition_ResolvesThePortAndTheChecks()
    {
        using var provider = Composition().BuildServiceProvider();
        using var scope = provider.CreateScope();

        scope.ServiceProvider.GetRequiredService<ITicketExistence>().Should().BeOfType<TicketExistence>();
        scope.ServiceProvider.GetRequiredService<ProposalMechanicalChecks>().Should().NotBeNull();
    }

    [Fact]
    public async Task MechanicalChecks_TwoMissingTickets_BothNamedUnderTheirRequiringTickets()
    {
        var existence = new ScriptedExistence(["41", "PROJ-7"]);
        var pipeline = WithProject();

        var checkedList = await ProposalReviewRig.Checks(existence).CheckAsync(pipeline,
            List(Ticket("order-api", "ticket:#41"), Ticket("order-ui", "order-api", "ticket:PROJ-7")), CancellationToken.None);

        existence.Checked.Should().ContainSingle().Which.Should().Equal("41", "PROJ-7");
        checkedList.Findings.Select(f => (f.SpecId, f.Problem, f.Why)).Should().Equal(
            ("order-api", "required ticket not found", "ticket 41 does not exist on gh-tracker"),
            ("order-ui", "required ticket not found", "ticket PROJ-7 does not exist on gh-tracker"));
    }

    [Fact]
    public async Task MechanicalChecks_UnreadableTicket_NamedWithItsClass()
    {
        var existence = new ScriptedExistence([], new UnreadableTicket("41", "unauthorized"));

        var checkedList = await ProposalReviewRig.Checks(existence).CheckAsync(WithProject(),
            List(Ticket("order-api", "ticket:41"), Ticket("order-ui", "order-api")), CancellationToken.None);

        checkedList.Findings.Should().ContainSingle().Which.Why.Should().Be("ticket 41 could not be read (unauthorized)");
    }

    [Fact]
    public async Task MechanicalChecks_NoExistenceRegistered_ChecksNothing()
    {
        var checkedList = await ProposalReviewRig.Checks().CheckAsync(WithProject(),
            List(Ticket("order-api", "ticket:41"), Ticket("order-ui", "order-api")), CancellationToken.None);

        checkedList.Findings.Should().BeEmpty();
    }

    [Fact]
    public async Task MechanicalChecks_AnAnswer_IsReturnedAsItIs()
    {
        var answer = new AnswerOutcome();

        (await ProposalReviewRig.Checks().CheckAsync(new PipelineContext(), answer, CancellationToken.None))
            .Should().BeSameAs(answer);
    }

    private static GraphTicket Ticket(string key, params string[] requires) => Validation.TicketGraphFixtures.Ticket(key, requires);

    private static TicketGraphOutcome List(params GraphTicket[] tickets) => new(tickets);

    private static PipelineContext WithProject()
    {
        var pipeline = new PipelineContext();
        pipeline.Set(ContextKeys.SpecDialogProject,
            new ResolvedProject { Name = "shop", Tracker = new TrackerConnection { Name = "gh-tracker" } });
        return pipeline;
    }

    private static ServiceCollection Composition()
    {
        var services = new ServiceCollection();
        services.AddLogging(builder => builder.AddProvider(NullLoggerProvider.Instance));
        ServerCompositionBuilder.ConfigureServices(services, "agentsmith.yml");
        return services;
    }

    /// <summary>Answers missing and unreadable ids as scripted and records every check.</summary>
    private sealed class ScriptedExistence(IReadOnlyList<string> missing, params UnreadableTicket[] unreadable) : ITicketExistence
    {
        public List<IReadOnlyList<string>> Checked { get; } = [];

        public Task<TicketExistenceResult> CheckAsync(ResolvedProject project, IReadOnlyList<string> ids, CancellationToken ct)
        {
            Checked.Add(ids);
            return Task.FromResult(new TicketExistenceResult(
                [.. missing.Where(ids.Contains)], [.. unreadable.Where(u => ids.Contains(u.Id))]));
        }
    }
}
