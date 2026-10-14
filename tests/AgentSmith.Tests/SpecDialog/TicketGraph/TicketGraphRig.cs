using System.Net;
using AgentSmith.Application.Services.Claim;
using AgentSmith.Application.Services.Persistence;
using AgentSmith.Application.Services.Sandbox;
using AgentSmith.Application.Services.SpecDialog;
using AgentSmith.Application.Services.Specs;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Contracts.Services;
using AgentSmith.Contracts.Specs;
using AgentSmith.Contracts.Tickets;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Exceptions;
using AgentSmith.Domain.Models;
using AgentSmith.Server.Models;
using AgentSmith.Server.Services.SpecDialog;
using AgentSmith.Tests.TestSupport;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.VisualStudio.Services.Common;
using Moq;

namespace AgentSmith.Tests.SpecDialog.TicketGraph;

/// <summary>
/// 2026-10-14-dbcel: a filed list over a fake tracker of a chosen type — which throws, for a ticket it does not have,
/// what that tracker's real client throws — the in-memory requirement and landing stores, the in-memory approval
/// store, the REAL existence read, orderer, starter and resolver. The project resolves by the tag "operator-tag" and a
/// ticket starts in "To Do"; a created ticket sits in "New", so a started one is tagged AND moved.
/// </summary>
internal sealed class TicketGraphRig
{
    public const string ConversationId = "conv-1";
    public const string Tag = "operator-tag";

    public TicketGraphRig(TrackerType type = TrackerType.AzureDevOps)
    {
        Type = type;
        Tracker = new GraphTracker(type);
        Approvals = new WatchedApprovals(new InMemorySpecApprovalStore());
        Requirements = new WatchedRequirements(Facts);
    }

    public TrackerType Type { get; }
    public GraphTracker Tracker { get; }
    public InMemoryTicketFacts Facts { get; } = new();
    public WatchedApprovals Approvals { get; }
    public WatchedRequirements Requirements { get; }
    public IReferenceSetReader References { get; set; } = new NoReferenceSetReader();
    public ResolvedProject Project => Config().Projects["proj"];

    public TicketGraphFiler Filer()
    {
        var factory = new Mock<ITicketProviderFactory>();
        factory.Setup(f => f.Create(It.IsAny<TrackerConnection>())).Returns(Tracker);
        var config = Config();
        return new TicketGraphFiler(config, factory.Object, new TicketExistence(factory.Object, NullLogger<TicketExistence>.Instance),
            new TicketGraphOrderer(), Requirements,
            new GraphTicketApproval(Approvals, References, new SeriesRepos(), TimeProvider.System, NullLogger<GraphTicketApproval>.Instance),
            Starts(config), ApprovedSetDoubles.Kinds(), TimeProvider.System, NullLogger<TicketGraphFiler>.Instance);
    }

    public TicketGraphStarts Starts(AgentSmithConfig? config = null) => new(
        new UnmetTicketRequirements(Requirements, Facts, Approvals), FiledWorkDoubles.Starter(config ?? Config()),
        NullLogger<TicketGraphStarts>.Instance);

    public Task<FilingReport> FileAsync(TicketGraphOutcome graph, bool mayStartRuns = true, CancellationToken ct = default) =>
        Filer().FileAsync(State(), graph, mayStartRuns, ct);

    public string KeyOf(string id) => TicketKey.For(Type.ToString().ToLowerInvariant(), id).Value;

    public void Land(string id) => Facts.Landed.Add(new TicketLandingFact(
        "sample-tracker", KeyOf(id), id, "proj", TicketLandingKinds.Merged, DateTimeOffset.UnixEpoch, null, null, null));

    public static ConversationState State(string? ticketKey = null) => new()
    {
        JobId = ConversationId, ChannelId = "C1", UserId = "sample.user", Platform = "dashboard",
        Project = "proj", TicketId = string.Empty, StartedAt = DateTimeOffset.UtcNow, TicketKey = ticketKey,
        Scope = new ActiveScope { Project = "proj", Repos = ["sample-api", "sample-web"] },
    };

    public static GraphTicket Ticket(string key, params string[] requires) =>
        new(key, $"Title {key}", $"Requirement of {key}.", [$"{key} works", $"{key} is tested"], requires, []);

    public static TicketGraphOutcome List(params GraphTicket[] tickets) => new(tickets);

    public AgentSmithConfig Config()
    {
        var trigger = new WebhookTriggerConfig
        {
            DefaultPipeline = "code",
            TriggerStatuses = [Type == TrackerType.GitHub ? "open" : "To Do"],
            ProjectResolution = new ProjectResolutionConfig { Strategy = ResolutionStrategy.Tag, Value = Tag },
        };
        var project = new ResolvedProject
        {
            Name = "proj", DefaultPipeline = "code",
            Tracker = new TrackerConnection { Name = "sample-tracker", Type = Type },
            Repos = [new RepoConnection { Name = "sample-api" }, new RepoConnection { Name = "sample-web" }],
            GithubTrigger = Type == TrackerType.GitHub ? trigger : null,
            GitlabTrigger = Type == TrackerType.GitLab ? trigger : null,
            AzuredevopsTrigger = Type == TrackerType.AzureDevOps ? trigger : null,
            JiraTrigger = Type == TrackerType.Jira ? new JiraTriggerConfig
            {
                DefaultPipeline = "code", TriggerStatuses = trigger.TriggerStatuses, ProjectResolution = trigger.ProjectResolution,
            } : null,
        };
        return new AgentSmithConfig
        {
            Projects = new Dictionary<string, ResolvedProject>(StringComparer.Ordinal) { ["proj"] = project },
            PipelineTriggers = PipelineTriggerMap.Empty,
        };
    }
}

/// <summary>A tracker of one type: creates tickets numbered from 101 (PROJ-101 on Jira), reads back what it created
/// or was told exists, and throws its real client's not-found for anything else.</summary>
internal sealed class GraphTracker(TrackerType type) : ITicketProvider
{
    private readonly Dictionary<string, string> _status = new(StringComparer.Ordinal);

    public List<(string Id, string Title, string Body, IReadOnlyList<string> Labels)> Created { get; } = [];
    public List<(string Ticket, string Status)> Moves { get; } = [];
    public List<(string Ticket, string Label)> Labels { get; } = [];
    public HashSet<string> Existing { get; } = new(StringComparer.Ordinal);
    public Dictionary<string, Exception> ReadFailures { get; } = new(StringComparer.Ordinal);
    public int? FailCreateAt { get; set; }
    public Action<int>? AfterCreate { get; set; }

    public string ProviderType => type.ToString().ToLowerInvariant();

    public Task<ConnectionProbeResult> ProbeAsync(CancellationToken cancellationToken) =>
        Task.FromResult(ConnectionProbeResult.Reachable(0));

    public Task<Ticket> GetTicketAsync(TicketId ticketId, CancellationToken cancellationToken)
    {
        if (ReadFailures.TryGetValue(ticketId.Value, out var failure)) throw failure;
        if (!Existing.Contains(ticketId.Value) && !_status.ContainsKey(ticketId.Value)) throw Missing(ticketId.Value);
        return Task.FromResult(new Ticket(ticketId, "t", string.Empty, null,
            _status.GetValueOrDefault(ticketId.Value, "New"), ProviderType, []));
    }

    public Task<CreatedTicket> CreateAsync(
        string title, string description, IReadOnlyList<string> labels, string? kind, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        if (FailCreateAt == Created.Count) throw new InvalidOperationException("the tracker refused the create");
        var n = 101 + Created.Count;
        var id = type == TrackerType.Jira ? $"PROJ-{n}" : n.ToString(System.Globalization.CultureInfo.InvariantCulture);
        Created.Add((id, title, description, labels));
        _status[id] = type == TrackerType.GitHub ? "open" : "New";
        AfterCreate?.Invoke(Created.Count);
        return Task.FromResult(new CreatedTicket(new TicketId(id), $"https://tracker.test/{id}"));
    }

    public Task<bool> AddLabelAsync(TicketId ticketId, string label, CancellationToken ct)
    {
        Labels.Add((ticketId.Value, label));
        return Task.FromResult(true);
    }

    public Task<bool> TransitionToAsync(TicketId ticketId, string statusName, CancellationToken ct)
    {
        Moves.Add((ticketId.Value, statusName));
        _status[ticketId.Value] = statusName;
        return Task.FromResult(true);
    }

    public Task<TicketFinalizeResult> FinalizeAsync(TicketId ticketId, string comment, string? doneStatus, CancellationToken ct) =>
        Task.FromResult(TicketFinalizeResult.Moved());

    /// <summary>What each real client throws for a ticket its tracker does not have.</summary>
    private Exception Missing(string id) => type switch
    {
        TrackerType.AzureDevOps => new VssServiceException($"TF401232: Work item {id} does not exist, or you do not have permissions to read it."),
        TrackerType.GitHub => new Octokit.NotFoundException("Not Found", HttpStatusCode.NotFound),
        TrackerType.Jira => new HttpRequestException("404 from https://jira.internal.example", null, HttpStatusCode.NotFound),
        _ => new TicketNotFoundException(new TicketId(id)),
    };
}

/// <summary>The approval store, recording how many edges were stored when each record was saved; it can refuse a save.</summary>
internal sealed class WatchedApprovals(InMemorySpecApprovalStore inner) : ISpecApprovalStore
{
    public Func<int>? EdgesSoFar { get; set; }
    public List<int> EdgesWhenSaved { get; } = [];
    public int? FailSaveAt { get; set; }
    public List<SpecApprovalRecord> Saved { get; } = [];

    public Task<SpecApprovalRecord?> GetAsync(string tracker, string key, CancellationToken ct) => inner.GetAsync(tracker, key, ct);

    public Task SaveAsync(SpecApprovalRecord record, CancellationToken ct)
    {
        if (FailSaveAt == Saved.Count) throw new InvalidOperationException("the approvals table is gone");
        EdgesWhenSaved.Add(EdgesSoFar?.Invoke() ?? 0);
        Saved.Add(record);
        return inner.SaveAsync(record, ct);
    }

    public Task<OutstandingApprovals> ListOutstandingAsync(string tracker, int limit, CancellationToken ct) =>
        inner.ListOutstandingAsync(tracker, limit, ct);

    public Task MarkSatisfiedAsync(string tracker, string key, DateTimeOffset at, CancellationToken ct) =>
        inner.MarkSatisfiedAsync(tracker, key, at, ct);

    public Task ReopenAsync(string tracker, string key, CancellationToken ct) => inner.ReopenAsync(tracker, key, ct);
}

/// <summary>The requirement store over the in-memory facts; it can refuse a write.</summary>
internal sealed class WatchedRequirements(InMemoryTicketFacts facts) : ITicketRequirementStore
{
    public bool Fail { get; set; }

    public Task<int> AddAsync(IReadOnlyList<TicketRequirementEdge> edges, CancellationToken cancellationToken) =>
        Fail ? throw new InvalidOperationException("the requirements table is gone") : facts.AddAsync(edges, cancellationToken);

    public Task<IReadOnlyList<TicketRequirementEdge>> RequiredOfAsync(string tracker, string ticketKey, CancellationToken cancellationToken) =>
        facts.RequiredOfAsync(tracker, ticketKey, cancellationToken);

    public Task<IReadOnlyList<TicketRequirementEdge>> DependentsOfAsync(string tracker, string requiredTicketKey, CancellationToken cancellationToken) =>
        facts.DependentsOfAsync(tracker, requiredTicketKey, cancellationToken);

    public Task<IReadOnlyList<TicketRequirementEdge>> ByProjectAsync(string project, CancellationToken cancellationToken) =>
        facts.ByProjectAsync(project, cancellationToken);
}
