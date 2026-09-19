using AgentSmith.Application.Services.SpecDialog;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Models;
using AgentSmith.Server.Models;
using AgentSmith.Server.Services.SpecDialog;
using AgentSmith.Tests.TestSupport;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AgentSmith.Tests.SpecDialog;

/// <summary>
/// 2026-09-18-d518: a ticket the framework files says what its LABELS bind — the one thing on it
/// a person cannot read off the body, because the labels route the ticket and guard its
/// specification from somewhere the body never mentions.
/// </summary>
public sealed class TicketLabelNoteFilingTests
{
    [Fact]
    public async Task Filing_APhaseTicket_CarriesTheNoteAsProseUnderItsOwnHeading()
    {
        var provider = new RecordingProvider();

        await FileAsync(provider, new PhaseOutcome(Draft("p9000a")));

        var body = provider.Created.Single().Body;
        body.Should().Contain(TicketLabelNote.Heading);
        var lines = Note(body);
        lines.Should().HaveCount(2, "one sentence per label the ticket actually carries")
            .And.OnlyContain(line => !line.StartsWith('-') && !line.StartsWith('*'),
                "bare prose, because a bulleted line under a heading can be read as a criterion");
        lines[0].Should().Contain(PhaseTicketRenderer.PhaseLabel)
            .And.Contain("binds this ticket to phase execution")
            .And.Contain("routed by this project's own rules, or dropped",
                "the MECHANISM is true on every routing shape; an outcome is not");
        lines[1].Should().Contain(FiledTicketLabels.ApprovedSetStamp)
            .And.Contain("Removing it costs", "the stamp is the guard that fails open");
    }

    [Fact]
    public async Task Filing_TheEmittedNote_IsWrappedInTheMarkerPairTheStripperLooksFor()
    {
        var provider = new RecordingProvider();

        await FileAsync(provider, new EpicOutcome(Draft("p9000"), [Draft("p9000a")]));

        var work = provider.Created[0].Body;
        work.Should().Contain(TicketLabelNote.Begin).And.Contain(TicketLabelNote.End);
        TicketLabelNote.Begin.Should().StartWith("<!-- " + TicketLabelNote.BeginIdentifier)
            .And.EndWith("-->");
        Inside(TicketLabelNote.Begin).Should().NotContain("--",
            "an HTML comment may not contain a double hyphen, so the separator stays a single one");
        AgentSmith.Application.Services.Specs.TicketLabelNoteStripper.Strip(work)
            .Should().NotContain(TicketLabelNote.Heading,
                "the pair the renderer writes is the pair the stripper takes");
        provider.Created[1].Body.Should().NotContain(TicketLabelNote.BeginIdentifier,
            "a slice record carries one record label and never reaches the stamp gate");
    }

    [Fact]
    public async Task Filing_ABugTicket_CarriesNoNote()
    {
        var provider = new RecordingProvider();

        await FileAsync(provider, new BugOutcome(new BugTicketDraft("Login 500s", "It throws.", null)));

        provider.Created.Single().Labels.Should().BeEmpty();
        provider.Created.Single().Body.Should().NotContain(TicketLabelNote.BeginIdentifier)
            .And.NotContain(TicketLabelNote.Heading,
                "a bug is filed with no labels, so a note about labels would be false");
    }

    /// <summary>A comment's text, without its delimiters.</summary>
    private static string Inside(string comment) => comment[4..^3];

    /// <summary>The note's own lines — what stands between the heading and the end marker.</summary>
    private static IReadOnlyList<string> Note(string body) =>
        [.. body.Split('\n')
            .SkipWhile(l => !l.StartsWith(TicketLabelNote.Heading, StringComparison.Ordinal))
            .Skip(1)
            .TakeWhile(l => !l.StartsWith("<!--", StringComparison.Ordinal))
            .Select(l => l.Trim())
            .Where(l => l.Length > 0)];

    private static async Task FileAsync(RecordingProvider provider, OutcomeProposal proposal)
    {
        var factory = new Mock<ITicketProviderFactory>();
        factory.Setup(f => f.Create(It.IsAny<TrackerConnection>())).Returns(provider);
        var store = ApprovedSetDoubles.Store();
        var filer = new OutcomeTicketFiler(
            Config(), factory.Object, new PhaseTicketRenderer(), new BugTicketRenderer(),
            ApprovedSetDoubles.EpicFiler(store), ApprovedSetDoubles.Recorder(store),
            FiledWorkDoubles.Starter(), NullLogger<OutcomeTicketFiler>.Instance);

        var report = await filer.FileAsync(State(), proposal, false, CancellationToken.None);

        report.Error.Should().BeNull();
    }

    private static PhaseDraft Draft(string id) =>
        new(id, $"phase {id}",
            $"phase: {id}\ngoal: \"phase {id}\"\ndone:\n  - \"{id} is finished\"",
            []) { Done = [$"{id} is finished"] };

    private static AgentSmithConfig Config() => new()
    {
        Projects = new Dictionary<string, ResolvedProject>
        {
            ["proj"] = new()
            {
                Name = "proj",
                Tracker = new TrackerConnection { Name = "sample-tracker", Type = TrackerType.AzureDevOps },
                Repos = [new RepoConnection { Name = "sample-api" }],
            },
        },
    };

    private static ConversationState State() => new()
    {
        JobId = "job-1",
        ChannelId = "C1",
        UserId = "sample.user",
        Platform = "dashboard",
        Project = "proj",
        TicketId = string.Empty,
        StartedAt = DateTimeOffset.UtcNow,
    };

    private sealed class RecordingProvider : ITicketProvider
    {
        private readonly List<(string Title, string Body, IReadOnlyList<string> Labels)> _created = [];

        public IReadOnlyList<(string Title, string Body, IReadOnlyList<string> Labels)> Created => _created;

        public string ProviderType => "recording";

        public Task<ConnectionProbeResult> ProbeAsync(CancellationToken cancellationToken) =>
            Task.FromResult(ConnectionProbeResult.Reachable(0));

        public Task<Ticket> GetTicketAsync(TicketId ticketId, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<CreatedTicket> CreateAsync(
            string title, string description, IReadOnlyList<string> labels, CancellationToken cancellationToken)
        {
            _created.Add((title, description, labels));
            return Task.FromResult(new CreatedTicket(
                new TicketId(_created.Count.ToString()), $"https://tracker.test/{_created.Count}"));
        }

        public Task<ParentLinkResult> LinkToParentAsync(
            CreatedTicket child, TicketId parent, CancellationToken cancellationToken) =>
            Task.FromResult(ParentLinkResult.Linked);

        public Task UpdateStatusAsync(TicketId ticketId, string comment, CancellationToken cancellationToken) =>
            Task.CompletedTask;

        public Task<TicketFinalizeResult> FinalizeAsync(
            TicketId ticketId, string comment, string? doneStatus, CancellationToken cancellationToken) =>
            Task.FromResult(TicketFinalizeResult.Moved());
    }
}
