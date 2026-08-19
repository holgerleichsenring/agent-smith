using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Models;

namespace AgentSmith.Tests.TestHelpers;

/// <summary>
/// p0461: the world a parked run's ticket lives in — one project, one tracker, one comment
/// thread. Shared by the answer-collection and status-move cases because they are two halves
/// of the same relationship and would otherwise build the same scaffolding twice.
/// </summary>
public static class ParkedTicketFixture
{
    public const string ProjectName = "sample";
    public const string TicketNumber = "19213";

    public static ServerContext Context { get; } = new("agentsmith.yml");

    public static RunCheckpointRecord Checkpoint(DateTimeOffset asked) => new(
        RunId: "2026-08-19T10-00-00-48ca", Project: ProjectName, TicketId: TicketNumber,
        Platform: "azuredevops", Pipeline: "fix-bug", DialogueJobId: "job-1", QuestionId: "q1",
        QuestionJson: "{}", RemainingCommandsJson: "[]", ContextJson: "[]", ExecutionCount: 1,
        AskedAt: asked, AnswerDeadlineAt: asked.AddDays(14), ResumedAt: null);

    public static IConfigurationLoader Loader(
        TrackerType type = TrackerType.AzureDevOps, string? inProgressStatus = null)
    {
        var trigger = new WebhookTriggerConfig
        {
            TriggerStatuses = ["To Do"],
            InProgressStatus = inProgressStatus,
        };
        var project = new ResolvedProject
        {
            Name = ProjectName,
            Tracker = new TrackerConnection { Name = "tracker", Type = type },
            AzuredevopsTrigger = type == TrackerType.AzureDevOps ? trigger : null,
            GithubTrigger = type == TrackerType.GitHub ? trigger : null,
        };
        var config = new AgentSmithConfig { Projects = { [ProjectName] = project } };
        return new FixedLoader(config);
    }

    public static (ITicketProviderFactory Factory, RecordingTicket Ticket) Provider(
        IReadOnlyList<TicketComment>? comments = null)
    {
        var ticket = new RecordingTicket(comments ?? []);
        return (new FixedFactory(ticket), ticket);
    }

    private sealed class FixedLoader(AgentSmithConfig config) : IConfigurationLoader
    {
        public AgentSmithConfig LoadConfig(string configPath) => config;

        public ConfigFileReadFact? LastRead => null;
    }

    private sealed class FixedFactory(ITicketProvider provider) : ITicketProviderFactory
    {
        public ITicketProvider Create(TrackerConnection config) => provider;
    }

    /// <summary>The ticket as a recorder: what it was asked for, and what was written to it.</summary>
    public sealed class RecordingTicket(IReadOnlyList<TicketComment> comments) : ITicketProvider
    {
        public List<string> Transitions { get; } = [];

        public string ProviderType => "AzureDevOps";

        public Task<IReadOnlyList<TicketComment>> GetCommentsAsync(
            TicketId ticketId, CancellationToken cancellationToken = default) =>
            Task.FromResult(comments);

        public Task TransitionToAsync(
            TicketId ticketId, string statusName, CancellationToken cancellationToken)
        {
            Transitions.Add(statusName);
            return Task.CompletedTask;
        }

        public Task<ConnectionProbeResult> ProbeAsync(CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<Ticket> GetTicketAsync(
            TicketId ticketId, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<CreatedTicket> CreateAsync(
            string title, string description, IReadOnlyList<string> labels,
            CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task FinalizeAsync(
            TicketId ticketId, string comment, string? doneStatus,
            CancellationToken cancellationToken) => throw new NotSupportedException();
    }
}
