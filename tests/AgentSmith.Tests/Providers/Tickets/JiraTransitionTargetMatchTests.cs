using System.Net;
using System.Text.Json;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Domain.Models;
using AgentSmith.Infrastructure.Services.Providers.Tickets;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentSmith.Tests.Providers.Tickets;

/// <summary>
/// 2026-09-17-042eg: a Jira workflow names its transitions for the ACT ("Start work"), not always
/// for the destination ("In Progress"). The name match stays exactly as it was — close, finalize,
/// lifecycle and retry all rest on it — and only when no name matches is the transition whose
/// target status IS the wanted status taken, so a filed ticket can reach a trigger status the
/// operator configured perfectly well.
/// </summary>
public sealed class JiraTransitionTargetMatchTests
{
    private const string Transitions = """
        {"transitions":[
          {"id":"11","name":"Start work","to":{"name":"In Progress"}},
          {"id":"21","name":"Finish","to":{"name":"Done"}}
        ]}
        """;

    [Fact]
    public async Task JiraTransition_NameMatch_StillWinsOverTarget()
    {
        // "Finish" matches by NAME; a transition whose TARGET is also "Finish" sits before it and
        // must not win — every transition a name match found before is the one taken now.
        var handler = new TransitionsHandler("""
            {"transitions":[
              {"id":"11","name":"Start work","to":{"name":"Finish"}},
              {"id":"21","name":"Finish","to":{"name":"Done"}}
            ]}
            """);

        (await Transitioner(handler).TransitionAsync(
            new TicketId("PROJ-1"), "Finish", null, CancellationToken.None)).Should().BeTrue();

        Posted(handler).Should().Be("21");
    }

    [Fact]
    public async Task JiraTransition_NoNameMatchButTargetMatches_IsFound()
    {
        var handler = new TransitionsHandler(Transitions);

        var moved = await Transitioner(handler).TransitionAsync(
            new TicketId("PROJ-1"), "In Progress", null, CancellationToken.None);

        moved.Should().BeTrue();
        Posted(handler).Should().Be("11", "the transition that LANDS in the wanted status");
    }

    [Fact]
    public async Task JiraTransition_NeitherNameNorTargetMatches_IsStillNotFound()
    {
        var handler = new TransitionsHandler(Transitions);

        var moved = await Transitioner(handler).TransitionAsync(
            new TicketId("PROJ-1"), "Triage", null, CancellationToken.None);

        moved.Should().BeFalse("the ticket stays where it is, and the caller records by label");
        handler.Posts.Should().BeEmpty();
    }

    /// <summary>
    /// A name match is the answer even when the tracker sent it without an id: the transition the
    /// name found is still the one taken, so it answers "none" rather than falling through to a
    /// target pass the name match exists to pre-empt.
    /// </summary>
    [Fact]
    public async Task JiraTransition_NameMatchWithoutAnId_DoesNotFallThroughToTheTarget()
    {
        var handler = new TransitionsHandler("""
            {"transitions":[
              {"name":"In Progress","to":{"name":"Elsewhere"}},
              {"id":"21","name":"Start work","to":{"name":"In Progress"}}
            ]}
            """);

        var moved = await Transitioner(handler).TransitionAsync(
            new TicketId("PROJ-1"), "In Progress", null, CancellationToken.None);

        moved.Should().BeFalse("the name match decided, and it carried no id");
        handler.Posts.Should().BeEmpty();
    }

    [Fact]
    public async Task JiraFinalize_DoneStatusByTransitionName_StillTransitions()
    {
        var handler = new TransitionsHandler(Transitions);
        var provider = new JiraTicketProvider(
            new JiraTicketConnection("https://jira.example.com", "u@example.com", "t", "PROJ", null),
            new HttpClient(handler), new JiraFieldMapper(), NullLogger<JiraTicketProvider>.Instance);

        await provider.FinalizeAsync(new TicketId("PROJ-1"), "done here", "Finish", CancellationToken.None);

        Posted(handler).Should().Be("21", "the name match the finalize path has always used");
    }

    [Fact]
    public async Task JiraLifecycle_StatusReachableOnlyByTarget_IsRecordedNatively()
    {
        var handler = new TransitionsHandler(Transitions);
        var map = new JiraLifecycleStatusMap(
            new Dictionary<TicketLifecycleStatus, string> { [TicketLifecycleStatus.InProgress] = "In Progress" });

        var native = await new JiraNativeLifecycleTransitioner(Transitioner(handler), map)
            .TryTransitionAsync(new TicketId("PROJ-1"), TicketLifecycleStatus.InProgress, CancellationToken.None);

        native.Should().BeTrue("a status no transition is NAMED for is still reachable, so no label is needed");
        Posted(handler).Should().Be("11");
    }

    private static JiraTransitioner Transitioner(TransitionsHandler handler) =>
        new(TicketProviderHttpClient.WithBasicAuth(
                new HttpClient(handler), "u@example.com", "token"),
            "https://jira.example.com", new JiraEndpoints(), NullLoggerFactory.Instance.CreateLogger("jira"));

    private static string? Posted(TransitionsHandler handler)
    {
        if (handler.Posts.Count == 0) return null;
        using var doc = JsonDocument.Parse(handler.Posts[^1]);
        return doc.RootElement.GetProperty("transition").GetProperty("id").GetString();
    }

    /// <summary>Answers GET /transitions with a fixed catalog and remembers every POST body.</summary>
    private sealed class TransitionsHandler(string catalog) : HttpMessageHandler
    {
        public List<string> Posts { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (request.Method == HttpMethod.Post && request.Content is not null)
            {
                var body = await request.Content.ReadAsStringAsync(cancellationToken);
                if (request.RequestUri!.AbsolutePath.EndsWith("/transitions", StringComparison.Ordinal))
                    Posts.Add(body);
            }

            return request.Method == HttpMethod.Get
                ? new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(catalog, System.Text.Encoding.UTF8, "application/json"),
                }
                : new HttpResponseMessage(HttpStatusCode.NoContent);
        }
    }
}
