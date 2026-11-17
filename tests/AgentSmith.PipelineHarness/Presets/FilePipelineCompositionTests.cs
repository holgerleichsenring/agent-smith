using AgentSmith.Application.Services.Filing;
using AgentSmith.Contracts.Events;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Specs;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Models;
using AgentSmith.PipelineHarness.Composition;
using AgentSmith.Server.Models;
using AgentSmith.Server.Services.SpecDialog;
using AgentSmith.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace AgentSmith.PipelineHarness.Presets;

/// <summary>
/// 2026-11-17-5701b: an approved list files through ONE 'file' run of the REAL server composition — its prologue, its
/// two steps, the database-backed approval, pointer and requirement stores. The tracker and the source remote are the
/// harness boundaries. The tracker triggers on "open" and the project resolves by its own tag.
/// </summary>
[Trait("Category", "PipelineHarness")]
public sealed class FilePipelineCompositionTests
{
    private const string Project = "fixture-file";
    private static readonly DateTimeOffset Noon = new(2026, 11, 17, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task FileRun_ApprovedListOfTwoTicketsWithSpecs_FilesBothRecordedAndWritten_FirstStartedSecondHeld()
    {
        var (tracker, events) = (new FilingTracker(), new RecordingEvents());
        await using var harness = RealCompositionHarness.Build(FixturePaths.For(FixturePaths.Filing), services =>
        {
            services.RemoveAll<ITicketProviderFactory>().AddSingleton<ITicketProviderFactory>(new FilingTrackerFactory(tracker));
            services.RemoveAll<IEventPublisher>().AddSingleton<IEventPublisher>(events);
        });
        await using var scope = harness.Services.CreateAsyncScope();

        var report = await scope.ServiceProvider.GetRequiredService<IFilingRun>().RunAsync(State(), List(), true, "prop-1");

        report.Error.Should().BeNull();
        events.Started.Should().ContainSingle(e => e.Pipeline == "file").Which.RunId.Should().NotBeNullOrEmpty();
        report.Filed.Select(f => f.Start!.State).Should().Equal(FiledStartState.Started, FiledStartState.Held);
        var services = scope.ServiceProvider;
        foreach (var id in new[] { "1", "2" })
        {
            var record = await services.GetRequiredService<ISpecApprovalStore>().GetAsync("github-filing", $"github-{id}", default);
            record!.Set.Specs.Should().ContainSingle();
            record.Approval!.Principal.Should().Be("sample.user");
            record.OwnerWords!.Select(w => w.Text).Should().Equal(["Split it into api and ui."], "the approve turn is no owner word");
            (await services.GetRequiredService<ISpecSetPointerStore>().GetAsync(Project, $"github-{id}", default))
                .Should().NotBeNull("the set is on the ticket's branch before anything starts it");
        }
        tracker.Created.Select(c => c.Labels.Count).Should().Equal(1, 1);
    }

    private static TicketGraphOutcome List() => new(
    [
        Ticket("api", []),
        Ticket("ui", ["api"]),
    ]);

    private static GraphTicket Ticket(string key, IReadOnlyList<string> requires) =>
        new(key, $"Title {key}", $"Requirement of {key}.", [$"{key} works"], requires, [])
        {
            Specs = [new SpecDraft($"{key}-spec", $"Goal of {key}", $"spec: {key}-spec\ngoal: \"Goal of {key}\"\n", [])],
        };

    private static ConversationState State() => new()
    {
        JobId = "s-file", ChannelId = "d-file", ThreadId = "d-file", UserId = "sample.user", Platform = "dashboard",
        Project = Project, TicketId = string.Empty, StartedAt = Noon,
        Transcript =
        [
            new TranscriptTurn(TranscriptRole.User, "Split it into api and ui.", Noon, Author: "sample.user"),
            new TranscriptTurn(TranscriptRole.Assistant, "Two tickets, ui after api.", Noon.AddMinutes(1)),
            new TranscriptTurn(TranscriptRole.User, "approve", Noon.AddMinutes(2), Decision: SpecDialogDecision.Approved,
                DecisionProposalId: "prop-1"),
        ],
    };

    private sealed class RecordingEvents : IEventPublisher
    {
        public List<RunStartedEvent> Started { get; } = [];

        public Task PublishAsync(RunEvent runEvent, CancellationToken cancellationToken = default)
        {
            if (runEvent is RunStartedEvent started) lock (Started) Started.Add(started);
            return Task.CompletedTask;
        }
    }

    /// <summary>Creates tickets "1", "2", … in status "open", takes every label.</summary>
    private sealed class FilingTracker : ITicketProvider
    {
        public List<(string Title, IReadOnlyList<string> Labels)> Created { get; } = [];

        public string ProviderType => "filing";

        public Task<ConnectionProbeResult> ProbeAsync(CancellationToken cancellationToken) =>
            Task.FromResult(ConnectionProbeResult.Reachable(0));

        public Task<Ticket> GetTicketAsync(TicketId ticketId, CancellationToken cancellationToken) =>
            Task.FromResult(new Ticket(ticketId, "t", "body", null, "open", ProviderType, []));

        public Task<CreatedTicket> CreateAsync(
            string title, string description, IReadOnlyList<string> labels, string? kind, CancellationToken cancellationToken)
        {
            lock (Created) Created.Add((title, labels));
            return Task.FromResult(new CreatedTicket(new TicketId($"{Created.Count}"), $"https://tracker.test/{Created.Count}"));
        }

        public Task<bool> AddLabelAsync(TicketId ticketId, string label, CancellationToken cancellationToken) =>
            Task.FromResult(true);

        public Task<IReadOnlyList<TicketComment>> GetCommentsAsync(TicketId ticketId, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<TicketComment>>([]);

        public Task<TicketFinalizeResult> FinalizeAsync(
            TicketId ticketId, string comment, string? doneStatus, CancellationToken cancellationToken) =>
            Task.FromResult(TicketFinalizeResult.Moved());
    }

    private sealed class FilingTrackerFactory(FilingTracker tracker) : ITicketProviderFactory
    {
        public ITicketProvider Create(TrackerConnection config) => tracker;

        public ITicketRewriter CreateRewriter(TrackerConnection config) => new HarnessTicketRewriter();

        public ITicketSearch CreateSearch(TrackerConnection config) => new HarnessTicketSearch();

        public ITicketLinkedWork CreateLinkedWork(TrackerConnection config) => new RecordingLinkedWork();
    }
}
