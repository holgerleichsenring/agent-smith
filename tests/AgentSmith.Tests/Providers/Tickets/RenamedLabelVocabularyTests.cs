using System.Net;
using System.Text;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Tickets;
using AgentSmith.Domain.Models;
using AgentSmith.Infrastructure.Services.Providers.Tickets;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentSmith.Tests.Providers.Tickets;

/// <summary>
/// 2026-09-25-3c7ac: the eight labels the framework writes take their names from the tracker whose
/// board they land on, and a rename leaves no ticket wearing two.
/// <para>
/// Jira and GitLab are the two that removed the LITERAL computed for the state they believed the
/// ticket was in — read as the historical word, removed as the configured one, which was not
/// there. Both now strip by predicate, as Azure DevOps and GitHub already did.
/// </para>
/// </summary>
public sealed class RenamedLabelVocabularyTests
{
    private static readonly Dictionary<string, string> Renamed = new()
    {
        ["in-progress"] = "board:working",
        ["pending"] = "board:waiting",
        [TicketLabelVocabulary.ApprovedSetKey] = "board:spec-approved",
    };

    [Fact]
    public void Vocabulary_ATrackerConfiguringNothing_IsTodaysNames()
    {
        var vocabulary = TicketLabelVocabulary.For(new TrackerConnection());

        vocabulary.For(TicketLifecycleStatus.InProgress).Should().Be("agent-smith:in-progress");
        vocabulary.ApprovedSetStamp.Should().Be("phase-spec:approved");
    }

    [Fact]
    public void Vocabulary_ARenamedWord_IsWrittenAndTheHistoricalOneIsStillRecognised()
    {
        var vocabulary = new TicketLabelVocabulary(Renamed);

        vocabulary.For(TicketLifecycleStatus.InProgress).Should().Be("board:working");
        vocabulary.IsLifecycleLabel("agent-smith:in-progress").Should().BeTrue(
            "tickets carrying the old word sit on boards nobody will migrate");
        vocabulary.IsLifecycleLabel("board:working").Should().BeTrue();
        vocabulary.IsApprovedSetStamp("phase-spec:approved").Should().BeTrue();
        vocabulary.IsApprovedSetStamp("board:spec-approved").Should().BeTrue();
    }

    [Fact]
    public void Vocabulary_AnUnnamedState_KeepsTodaysWord()
    {
        new TicketLabelVocabulary(Renamed).For(TicketLifecycleStatus.Done)
            .Should().Be("agent-smith:done", "a rename names what it names and nothing else");
    }

    [Fact]
    public async Task Jira_ARenamedBoard_WritesTheNewWordAndRemovesTheHistoricalOne()
    {
        var handler = new RecordingHandler(
            Json("""{"fields":{"labels":["agent-smith:enqueued","keep-me"]}}"""),
            new HttpResponseMessage(HttpStatusCode.NoContent));
        var sut = new JiraTicketStatusTransitioner(
            new JiraTicketConnection(
                "https://jira.test", "a@b.c", "token", "PROJ",
                Labels: new TicketLabelVocabulary(Renamed)),
            new JiraWorkflowCatalog(NullLogger<JiraWorkflowCatalog>.Instance),
            new HttpClient(handler),
            NullLogger<JiraTicketStatusTransitioner>.Instance);

        await sut.TransitionAsync(
            new TicketId("PROJ-1"), TicketLifecycleStatus.Enqueued,
            TicketLifecycleStatus.InProgress, CancellationToken.None);

        handler.LastBody.Should().Contain("board:working", "the configured word is what lands");
        handler.LastBody.Should().Contain("agent-smith:enqueued",
            "the word actually on the ticket is what gets removed — removing the configured one "
            + "would leave the historical word behind for ever");
        handler.LastBody.Should().NotContain("keep-me", "a label that is not ours is not touched");
    }

    [Fact]
    public async Task GitLab_ARenamedBoard_WritesTheNewWordAndRemovesTheHistoricalOne()
    {
        var handler = new RecordingHandler(
            Json("""{"labels":["agent-smith:enqueued","keep-me"]}"""),
            Json("""{"id":1}"""));
        var sut = new GitLabTicketStatusTransitioner(
            new GitLabTicketConnection(
                "https://gitlab.test", "group%2Fproject", "token",
                new TicketLabelVocabulary(Renamed)),
            new HttpClient(handler),
            NullLogger<GitLabTicketStatusTransitioner>.Instance);

        await sut.TransitionAsync(
            new TicketId("1"), TicketLifecycleStatus.Enqueued,
            TicketLifecycleStatus.InProgress, CancellationToken.None);

        handler.LastBody.Should().Contain("board:working");
        handler.LastBody.Should().Contain("agent-smith:enqueued");
        handler.LastBody.Should().NotContain("keep-me");
    }

    [Fact]
    public async Task Jira_ARenamedPendingWord_IsWhatTheCatchUpQueryAsksFor()
    {
        // The pending word is the one lifecycle label an OPERATOR writes by hand, to surface a
        // ticket the discovery query would not return. A rename that missed this query would make
        // every hand-marked ticket vanish from the poll.
        var handler = new RecordingHandler(Json(EmptySearch));
        var sut = new JiraTicketProvider(
            new JiraTicketConnection(
                "https://jira.test", "a@b.c", "token", "PROJ",
                Labels: new TicketLabelVocabulary(Renamed)),
            new HttpClient(handler), new JiraFieldMapper(),
            NullLogger<JiraTicketProvider>.Instance);

        await sut.ListByLifecycleStatusAsync(TicketLifecycleStatus.Pending, CancellationToken.None);

        handler.LastBody.Should().Contain("board:waiting").And.NotContain("agent-smith:pending");
    }

    private const string EmptySearch = "{\"issues\":[]}";

    private static HttpResponseMessage Json(string body) =>
        new(HttpStatusCode.OK) { Content = new StringContent(body, Encoding.UTF8, "application/json") };

    private sealed class RecordingHandler(params HttpResponseMessage[] responses) : HttpMessageHandler
    {
        private int _next;

        public string LastBody { get; private set; } = string.Empty;

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (request.Content is not null)
                LastBody = await request.Content.ReadAsStringAsync(cancellationToken);
            return responses[Math.Min(_next++, responses.Length - 1)];
        }
    }
}
