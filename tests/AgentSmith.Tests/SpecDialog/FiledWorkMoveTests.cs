using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Models.Triggers;
using AgentSmith.Contracts.Providers;
using AgentSmith.Application.Services.SpecDialog;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Models;
using AgentSmith.Server.Services.SpecDialog;
using AgentSmith.Tests.TestSupport;
using FluentAssertions;

namespace AgentSmith.Tests.SpecDialog;

/// <summary>
/// 2026-10-13-9a59m: an approval in a never-worked ticket's own conversation starts that EXISTING ticket the
/// way a filed one is started — the routing tag, the poller's resolution, the move — on every tracker.
/// </summary>
public sealed class FiledWorkMoveTests
{
    private const string Tag = "agentsmith-proj";

    [Theory]
    [InlineData(TrackerType.GitHub, "closed", "open")]
    [InlineData(TrackerType.GitLab, "closed", "opened")]
    [InlineData(TrackerType.AzureDevOps, "New", "To Do")]
    [InlineData(TrackerType.Jira, "Backlog", "Selected")]
    public async Task BoundApproval_NeverWorked_StartsTheFirstAttempt_OnEveryTracker(TrackerType tracker, string status, string trigger)
    {
        var provider = new ExistingTicket(status, ["customer-label"]);
        var (config, project) = Routing(tracker, trigger);

        var start = await FiledWorkDoubles.Move(config).StartExistingAsync(provider, project, "42", mayStartRuns: true, default);

        start.State.Should().Be(FiledStartState.Started, start.Reason);
        provider.Moves.Should().Equal(trigger);
        provider.Added.Should().Equal(Tag);
    }

    [Fact]
    public async Task FiledWorkMove_ExistingTicket_AddsRoutingTagOnly()
    {
        var provider = new ExistingTicket("open", ["bug"]);
        var (config, project) = Routing(TrackerType.GitHub, "open");

        var start = await FiledWorkDoubles.Move(config).StartExistingAsync(provider, project, "42", mayStartRuns: false, default);

        provider.Added.Should().Equal([Tag], "no spec label and no approved-set stamp, which would re-route the ticket");
        provider.Added.Should().NotContain(FiledTicketLabels.ApprovedSetStamp);
        start.State.Should().Be(FiledStartState.Started, "it already sits in a trigger status, resolved with its current labels");
        provider.Moves.Should().BeEmpty();
    }

    private static (AgentSmithConfig, ResolvedProject) Routing(TrackerType tracker, string trigger)
    {
        T Trigger<T>() where T : WebhookTriggerConfig, new() => new()
        {
            DefaultPipeline = "code", TriggerStatuses = [trigger],
            ProjectResolution = new ProjectResolutionConfig { Strategy = ResolutionStrategy.Tag, Value = Tag },
        };
        var project = new ResolvedProject
        {
            Name = "proj", DefaultPipeline = "code",
            Tracker = new TrackerConnection { Name = "t", Type = tracker },
            GithubTrigger = tracker == TrackerType.GitHub ? Trigger<WebhookTriggerConfig>() : null,
            GitlabTrigger = tracker == TrackerType.GitLab ? Trigger<WebhookTriggerConfig>() : null,
            AzuredevopsTrigger = tracker == TrackerType.AzureDevOps ? Trigger<WebhookTriggerConfig>() : null,
            JiraTrigger = tracker == TrackerType.Jira ? Trigger<JiraTriggerConfig>() : null,
        };
        return (new AgentSmithConfig
        {
            Projects = new Dictionary<string, ResolvedProject> { ["proj"] = project },
            PipelineTriggers = PipelineTriggerMap.Empty,
        }, project);
    }

    private sealed class ExistingTicket(string status, IReadOnlyList<string> labels) : ITicketProvider
    {
        private string _status = status;

        public List<string> Moves { get; } = [];
        public List<string> Added { get; } = [];
        public string ProviderType => "existing";

        public Task<Ticket> GetTicketAsync(TicketId ticketId, CancellationToken cancellationToken) =>
            Task.FromResult(new Ticket(ticketId, "t", string.Empty, null, _status, ProviderType, [.. labels, .. Added]));

        public Task<ConnectionProbeResult> ProbeAsync(CancellationToken cancellationToken) =>
            Task.FromResult(ConnectionProbeResult.Reachable(0));

        public Task<CreatedTicket> CreateAsync(
            string title, string description, IReadOnlyList<string> labels, string? kind, CancellationToken ct) =>
            throw new NotSupportedException("an existing ticket is started, never created");

        public Task<bool> AddLabelAsync(TicketId ticketId, string label, CancellationToken ct)
        {
            Added.Add(label);
            return Task.FromResult(true);
        }

        public Task<bool> TransitionToAsync(TicketId ticketId, string statusName, CancellationToken ct)
        {
            Moves.Add(statusName);
            _status = statusName;
            return Task.FromResult(true);
        }

        public Task UpdateStatusAsync(TicketId ticketId, string comment, CancellationToken ct) => Task.CompletedTask;

        public Task<TicketFinalizeResult> FinalizeAsync(TicketId ticketId, string comment, string? doneStatus, CancellationToken ct) =>
            Task.FromResult(TicketFinalizeResult.Moved());
    }
}
