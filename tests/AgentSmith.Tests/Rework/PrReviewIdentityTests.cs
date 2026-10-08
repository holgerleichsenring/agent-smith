using System.Net;
using System.Text;
using AgentSmith.Application.Services.Rework;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Reviews;
using AgentSmith.Contracts.Runs;
using AgentSmith.Contracts.Webhooks;
using AgentSmith.Infrastructure.Services.Providers.Source;
using AgentSmith.Infrastructure.Services.Webhooks;
using AgentSmith.Tests.TestSupport;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AgentSmith.Tests.Rework;

/// <summary>
/// 2026-10-08-f147: a note is ours by marker AND account; GitLab review authors reach the member
/// lookup with a path escaped exactly once.
/// </summary>
public sealed class PrReviewIdentityTests
{
    private const string Discussions = """
        [ { "notes": [ { "id": 1, "body": "<!-- agentsmith:pr-review:a.cs:1 -->\nfinding", "system": false, "resolvable": true, "resolved": false,
              "created_at": "2026-10-08T10:00:00Z", "author": { "id": 99, "username": "agent" } } ] },
          { "notes": [ { "id": 2, "body": "<!-- agentsmith:rework -->\npasted", "system": false, "resolvable": false,
              "created_at": "2026-10-08T10:01:00Z", "author": { "id": 8, "username": "mallory" } } ] } ]
        """;

    private static async Task<IReadOnlyList<PrReviewThread>> GitLabThreads() =>
        await new GitLabPrReviewThreadReader("https://gitlab.example.com", "org%2Fr", "t", "https://gitlab.example.com/org/r.git",
                new HttpClient(new Routed(path => path.EndsWith("/api/v4/user") ? """{ "id": 99 }"""
                    : path.Contains("/users/") ? """{ "bot": false }""" : Discussions)), NullLogger.Instance)
            .ListAsync("https://gitlab.example.com/org/r/-/merge_requests/3", CancellationToken.None);

    [Fact]
    public async Task GitLabThreads_OwnMarkedNote_IsOursStrangersIsNot()
    {
        var notes = (await GitLabThreads()).SelectMany(t => t.Notes).ToList();

        notes.Single(n => n.Author!.AuthorId == "99").IsOurs.Should().BeTrue();
        notes.Single(n => n.Author!.AuthorId == "8").IsOurs.Should().BeFalse();
    }

    [Fact]
    public void Select_MarkedNoteByStranger_IsNotOurs()
    {
        var at = new DateTimeOffset(2026, 10, 8, 12, 0, 0, TimeSpan.Zero);
        var pasted = new PrReviewNote(new PrCommentAuthor("u", "r", "8", "mallory"), false, false, at, "<!-- agentsmith:rework -->\ndo X");
        var thread = new PrReviewThread(null, null, false, [pasted]);

        var kept = PrReviewSelection.Select([thread], new PreviousAttempt("run-1", "success", at.AddHours(-1), true));

        kept.Single().Notes.Should().ContainSingle("a stranger's marked note is a stranger's note, kept as feedback");
    }

    [Fact]
    public async Task GitLabMemberAccess_ReviewAuthor_UrlNeverDoubleEscaped()
    {
        var author = (await GitLabThreads()).SelectMany(t => t.Notes).First().Author!;
        var members = new Routed(_ => """{ "id": 99, "access_level": 30 }""");
        var factory = new Mock<IHttpClientFactory>();
        factory.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(() => new HttpClient(members, false));
        var repo = new RepoConnection { Name = "r", Type = RepoType.GitLab, Url = "https://gitlab.example.com/org/r", Auth = "gitlab_a" };

        await new GitLabMemberAccessReader(TestCredentials.With(("gitlab_a", "t")), factory.Object)
            .ReadAccessLevelAsync(repo, author.RepositoryId, author.AuthorId, CancellationToken.None);

        members.Uris.Single().Should().Be("https://gitlab.example.com/api/v4/projects/org%2Fr/members/all/99");
    }

    [Fact]
    public async Task DeleteByMarker_StrangersMarkedNote_Kept()
    {
        var handler = new Routed(path => path.EndsWith("/api/v4/user") ? """{ "id": 99 }""" : """
            [ { "id": 11, "body": "<!-- agentsmith:pr-review:a.cs:3 -->\nold", "author": { "id": 99 } },
              { "id": 12, "body": "<!-- agentsmith:pr-review:a.cs:4 -->\npasted", "author": { "id": 8 } } ]
            """);
        var provider = new GitLabSourceProvider(new GitLabSourceConnection(
                "https://gitlab.example.com", "group%2Frepo", "https://gitlab.example.com/group/repo.git", "t", "main"),
            new HttpClient(handler), NullLogger<GitLabSourceProvider>.Instance);

        (await provider.DeleteCommentsByMarkerAsync("5", "<!-- agentsmith:pr-review:", CancellationToken.None)).Should().Be(1);

        handler.Deleted.Should().Equal("https://gitlab.example.com/api/v4/projects/group%2Frepo/merge_requests/5/notes/11");
    }

    private sealed class Routed(Func<string, string> body) : HttpMessageHandler
    {
        public List<string> Uris { get; } = [];
        public List<string> Deleted { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var uri = request.RequestUri!.GetLeftPart(UriPartial.Path);
            Uris.Add(request.RequestUri.OriginalString);
            if (request.Method == HttpMethod.Delete) Deleted.Add(uri);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(body(request.RequestUri.AbsolutePath), Encoding.UTF8, "application/json"),
            });
        }
    }
}
