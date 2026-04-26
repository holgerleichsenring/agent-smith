using System.Net;
using AgentSmith.Contracts.Models;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Models;
using AgentSmith.Infrastructure.Services.Providers.Tickets;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentSmith.Tests.Providers.Tickets;

public sealed class GitLabTicketProviderListByLifecycleTests
{
    [Fact]
    public async Task ListByLifecycleStatusAsync_RequestUrlIncludesUrlEncodedLifecycleLabelAndOpenedState()
    {
        var handler = new RecordingHandler
        {
            Responder = _ => JsonResponse("[]")
        };
        var sut = BuildSut(handler);

        await sut.ListByLifecycleStatusAsync(TicketLifecycleStatus.Pending, CancellationToken.None);

        handler.LastRequest.Should().NotBeNull();
        var url = handler.LastRequest!.RequestUri!.ToString();
        url.Should().Contain("/api/v4/projects/group%2Fproj/issues");
        url.Should().Contain("labels=agent-smith%3Apending");
        url.Should().Contain("state=opened");
    }

    [Fact]
    public async Task ListByLifecycleStatusAsync_TwoIssues_MapsToTicketsWithIidAsIdAndLabelsRoundTrip()
    {
        var handler = new RecordingHandler
        {
            Responder = _ => JsonResponse(
                """
                [
                  { "iid": 17, "title": "first", "description": "d1", "state": "opened",
                    "labels": ["agent-smith:pending", "bug"] },
                  { "iid": 18, "title": "second", "description": null, "state": "opened",
                    "labels": [] }
                ]
                """)
        };
        var sut = BuildSut(handler);

        var tickets = await sut.ListByLifecycleStatusAsync(
            TicketLifecycleStatus.Pending, CancellationToken.None);

        tickets.Should().HaveCount(2);
        tickets[0].Id.Value.Should().Be("17");
        tickets[0].Title.Should().Be("first");
        tickets[0].Description.Should().Be("d1");
        tickets[0].Labels.Should().BeEquivalentTo(new[] { "agent-smith:pending", "bug" });
        tickets[1].Id.Value.Should().Be("18");
        tickets[1].Description.Should().Be(""); // null description maps to empty
        tickets[1].Labels.Should().BeEmpty();
    }

    [Fact]
    public async Task ListByLifecycleStatusAsync_HttpError_ReturnsEmptyList()
    {
        var handler = new RecordingHandler
        {
            Responder = _ => new HttpResponseMessage(HttpStatusCode.Forbidden)
        };
        var sut = BuildSut(handler);

        var tickets = await sut.ListByLifecycleStatusAsync(
            TicketLifecycleStatus.Pending, CancellationToken.None);

        tickets.Should().BeEmpty();
    }

    [Fact]
    public async Task ListByLifecycleStatusAsync_NotFound_ReturnsEmptyList()
    {
        var handler = new RecordingHandler
        {
            Responder = _ => new HttpResponseMessage(HttpStatusCode.NotFound)
        };
        var sut = BuildSut(handler);

        var tickets = await sut.ListByLifecycleStatusAsync(
            TicketLifecycleStatus.Pending, CancellationToken.None);

        tickets.Should().BeEmpty();
    }

    private static GitLabTicketProvider BuildSut(HttpMessageHandler handler)
    {
        var httpClient = new HttpClient(handler);
        var loader = new GitLabAttachmentLoader(
            "https://gitlab.com", "group%2Fproj", "token", httpClient,
            NullLogger<GitLabAttachmentLoader>.Instance);
        return new GitLabTicketProvider(
            "https://gitlab.com", "group%2Fproj", "token", httpClient, loader);
    }

    private static HttpResponseMessage JsonResponse(string json) => new(HttpStatusCode.OK)
    {
        Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json")
    };

    private sealed class RecordingHandler : HttpMessageHandler
    {
        public HttpRequestMessage? LastRequest { get; private set; }
        public Func<HttpRequestMessage, HttpResponseMessage> Responder { get; set; }
            = _ => new HttpResponseMessage(HttpStatusCode.OK);

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastRequest = request;
            return Task.FromResult(Responder(request));
        }
    }
}
