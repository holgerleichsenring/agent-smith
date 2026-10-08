using System.Net;
using AgentSmith.Infrastructure.Services.Providers.Source;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentSmith.Tests.Rework;

/// <summary>2026-10-08-f147: GitLab's standing requests for changes, read from its own system notes.</summary>
public sealed class GitLabMrActReaderTests
{
    private const string MrUrl = "https://gitlab.example/o/r/-/merge_requests/3";

    private static GitLabMrActReader Reader(string notes) =>
        new("https://gitlab.example", "o%2Fr", "t", "https://gitlab.example/o/r.git",
            new HttpClient(new ScriptedGitLab(notes)), NullLogger.Instance);

    private static string Note(long id, long author, string body, string at, bool system = true) =>
        $$"""{ "id": {{id}}, "body": "{{body}}", "system": {{(system ? "true" : "false")}}, "created_at": "{{at}}", "author": { "id": {{author}}, "username": "u{{author}}" } }""";

    [Fact]
    public async Task GitLabActReader_RequestedChangesNote_ReturnsActAtCreatedAt()
    {
        var acts = await Reader($"[{Note(2, 9, "requested changes", "2026-10-08T12:00:00Z")}, {Note(1, 9, "requested changes", "2026-10-08T11:00:00Z")}]")
            .ChangesRequestedAsync(MrUrl, CancellationToken.None);

        var act = acts.Single();
        act.At.Should().Be(new DateTimeOffset(2026, 10, 8, 12, 0, 0, TimeSpan.Zero));
        act.IsSystem.Should().BeFalse();
        act.Author!.AuthorId.Should().Be("9");
        act.Author.RepositoryId.Should().Be("o/r");
    }

    [Fact]
    public async Task GitLabActReader_ApprovedNoteAfter_Empty()
    {
        var acts = await Reader($"[{Note(2, 9, "approved this merge request", "2026-10-08T12:00:00Z")}, {Note(1, 9, "requested changes", "2026-10-08T11:00:00Z")}]")
            .ChangesRequestedAsync(MrUrl, CancellationToken.None);

        acts.Should().BeEmpty();
    }

    [Fact]
    public async Task GitLabActReader_MrAuthorOrTokenUser_Ignored()
    {
        var acts = await Reader($"[{Note(3, 1, "requested changes", "2026-10-08T12:00:00Z")}, {Note(2, 2, "requested changes", "2026-10-08T12:00:00Z")}, {Note(1, 9, "requested changes", "2026-10-08T12:00:00Z", system: false)}]")
            .ChangesRequestedAsync(MrUrl, CancellationToken.None);

        acts.Should().BeEmpty("1 is the token's user, 2 the MR author, and a person's comment saying the words is no act");
    }

    private sealed class ScriptedGitLab(string notes) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var path = request.RequestUri!.AbsolutePath;
            var body = path.EndsWith("/api/v4/user") ? """{ "id": 1 }"""
                : path.Contains("/users/") ? """{ "bot": false }"""
                : path.EndsWith("/notes") ? notes
                : """{ "iid": 3, "author": { "id": 2 } }""";
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(body) });
        }
    }
}
