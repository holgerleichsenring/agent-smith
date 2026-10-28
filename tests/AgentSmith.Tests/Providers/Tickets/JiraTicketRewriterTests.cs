using System.Net;
using System.Text;
using System.Text.Json.Nodes;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Tickets;
using AgentSmith.Domain.Models;
using AgentSmith.Infrastructure.Services.Providers.Tickets;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentSmith.Tests.Providers.Tickets;

/// <summary>
/// 2026-10-28-7a69b: the Jira description rewrite works on the stored document. The region's
/// paragraphs are swapped or appended; every other node — a mention, a table, a marked link — goes
/// back exactly as it was read, which the text round trip it replaced could not do.
/// </summary>
public sealed class JiraTicketRewriterTests
{
    private const string Mention = """{"type":"paragraph","content":[{"type":"mention","attrs":{"id":"acc-1","text":"@Anna"}}]}""";
    private const string Table = """{"type":"table","content":[{"type":"tableRow","content":[]}]}""";

    private static readonly string Region = FramedTicketRegion.Wrap("## Goal\nthe widget stops dropping\n");

    [Fact]
    public async Task JiraRewrite_MarkedDescription_ReplacesOnlyTheRegionAndKeepsEveryOtherNode()
    {
        var old = Paragraphs(FramedTicketRegion.Wrap("## Goal\nthe widget drops\n"));
        var host = new Host(Description($"{Mention},{old},{Table}"));

        var result = await Sut(host).RewriteRegionAsync(new TicketId("BET-1"), Region, RegionPlacement.ReplaceOnly, default);

        result.Rewritten.Should().BeTrue();
        var content = host.Written!["fields"]!["description"]!["content"]!.AsArray();
        content.First()!.ToJsonString().Should().Be(JsonNode.Parse(Mention)!.ToJsonString());
        content.Last()!.ToJsonString().Should().Be(JsonNode.Parse(Table)!.ToJsonString());
        host.WrittenText.Should().Contain("the widget stops dropping").And.NotContain("the widget drops\"");
    }

    [Fact]
    public async Task JiraRewrite_UnmarkedDescriptionWithAppend_AppendsTheRegion()
    {
        var host = new Host(Description(Mention));

        var result = await Sut(host).RewriteRegionAsync(new TicketId("BET-1"), Region, RegionPlacement.ReplaceOrAppend, default);

        result.Rewritten.Should().BeTrue();
        var content = host.Written!["fields"]!["description"]!["content"]!.AsArray();
        content.First()!.ToJsonString().Should().Be(JsonNode.Parse(Mention)!.ToJsonString());
        host.WrittenText.Should().Contain(FramedTicketRegion.BeginIdentifier).And.Contain("the widget stops dropping");
    }

    [Fact]
    public async Task JiraRewrite_UnmarkedDescriptionReplaceOnly_IsRefused()
    {
        var host = new Host(Description(Mention));

        var result = await Sut(host).RewriteRegionAsync(new TicketId("BET-1"), Region, RegionPlacement.ReplaceOnly, default);

        result.Outcome.Should().Be(TicketRewriteOutcome.Unsupported);
        host.Written.Should().BeNull("nothing is written to a ticket the framework rendered whole");
    }

    [Fact]
    public async Task JiraRewrite_MarkerMovedIntoAPanel_IsRefusedNotAppended()
    {
        var panel = "{\"type\":\"panel\",\"content\":[" + Paragraphs(FramedTicketRegion.Wrap("old")) + "]}";
        var host = new Host(Description($"{Mention},{panel}"));

        var result = await Sut(host).RewriteRegionAsync(new TicketId("BET-1"), Region, RegionPlacement.ReplaceOrAppend, default);

        result.Outcome.Should().Be(TicketRewriteOutcome.Unsupported);
        host.Written.Should().BeNull("the framework's text is in the panel; a second region would duplicate it");
    }

    [Fact]
    public async Task JiraRewrite_DescriptionIsText_IsRefusedNamingTheFormat()
    {
        var host = new Host("""{"key":"BET-1","fields":{"description":"h2. Goal\nwiki text"}}""");

        var result = await Sut(host).RewriteRegionAsync(new TicketId("BET-1"), Region, RegionPlacement.ReplaceOrAppend, default);

        result.Outcome.Should().Be(TicketRewriteOutcome.Unsupported);
        result.Reason.Should().Contain("as text rather than as a document");
        host.Written.Should().BeNull();
    }

    private static JiraTicketRewriter Sut(Host host) => new(
        new JiraTicketConnection("https://site.example.test", "bot@site.example.test", "token", "BET"),
        new HttpClient(host), NullLogger<JiraTicketRewriter>.Instance);

    private static string Description(string nodes) =>
        "{\"key\":\"WF-1\",\"fields\":{\"description\":{\"type\":\"doc\",\"version\":1,\"content\":[" + nodes + "]}}}";

    // A filed region as the creator renders it: one paragraph per line.
    private static string Paragraphs(string text) => string.Join(",", text.Split('\n').Select(line => line.Length == 0
        ? """{"type":"paragraph","content":[]}"""
        : new JsonObject { ["type"] = "paragraph", ["content"] = new JsonArray(new JsonObject { ["type"] = "text", ["text"] = line }) }.ToJsonString()));

    private sealed class Host(string issue) : HttpMessageHandler
    {
        public JsonNode? Written { get; private set; }

        public string WrittenText => Written?.ToJsonString() ?? string.Empty;

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (request.Method == HttpMethod.Put)
                Written = JsonNode.Parse(await request.Content!.ReadAsStringAsync(cancellationToken));
            return new HttpResponseMessage(request.Method == HttpMethod.Put ? HttpStatusCode.NoContent : HttpStatusCode.OK)
            {
                Content = new StringContent(request.Method == HttpMethod.Put ? string.Empty : issue, Encoding.UTF8, "application/json"),
            };
        }
    }
}
