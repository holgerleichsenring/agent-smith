using System.Net;
using System.Text;
using AgentSmith.Contracts.Sweep;
using AgentSmith.Infrastructure.Models;
using AgentSmith.Infrastructure.Services.Providers.Source;
using AgentSmith.Server.Services.Sweep;
using AgentSmith.Tests.TestSupport;
using FluentAssertions;

namespace AgentSmith.Tests.Rework.Sweep;

/// <summary>
/// 2026-11-25-cc1f: polling lists every open GitHub pull request and GitLab merge request RefreshOnly under a name of its target,
/// head, draft, conflict and review state — reviewed or not — so any change re-reads its record once; the name never moves the cursor.
/// </summary>
public sealed class ChangedPullRequestRefreshNameTests
{
    private static readonly DateTimeOffset Since = new(2026, 11, 25, 9, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData("baseRefName", "agent-smith/series/1")]
    [InlineData("headRefOid", "def")]
    [InlineData("isDraft", "true")]
    [InlineData("mergeable", "CONFLICTING")]
    public async Task GitHub_AnyChangeOfThePullRequest_IsANewRefreshName(string field, string value)
    {
        var before = await GitHubNameAsync(Pr());
        var after = await GitHubNameAsync(Pr().Replace(Field(field, Original(field)), Field(field, value), StringComparison.Ordinal));

        after.Should().NotBe(before).And.StartWith("refresh|https://github.com/o/r/pull/1|");
        (await GitHubNameAsync(Pr())).Should().Be(before, "an unchanged pull request is not read again");
    }

    [Fact]
    public async Task GitHub_ANewReview_IsANewRefreshName_AndAnUnreviewedPullRequestIsListed()
    {
        var unreviewed = await GitHubNameAsync(Pr());
        var reviewed = await GitHubNameAsync(Pr("""{"state":"APPROVED","submittedAt":"2026-11-25T08:00:00Z"}"""));

        reviewed.Should().NotBe(unreviewed);
        unreviewed.Should().NotBeNull("a pull request nobody reviewed is listed too");
    }

    [Theory]
    [InlineData("\"target_branch\":\"main\"", "\"target_branch\":\"agent-smith/series/1\"")]
    [InlineData("\"sha\":\"abc\"", "\"sha\":\"def\"")]
    [InlineData("\"draft\":false", "\"draft\":true")]
    [InlineData("\"detailed_merge_status\":\"mergeable\"", "\"detailed_merge_status\":\"conflict\"")]
    public async Task GitLab_AnyChangeOfTheMergeRequest_IsANewRefreshName(string from, string to)
    {
        var before = (await GitLabAsync(Mr())).Single(i => i.Dedupe is not null);
        var after = (await GitLabAsync(Mr().Replace(from, to, StringComparison.Ordinal))).Single(i => i.Dedupe is not null);

        after.Dedupe.Should().NotBe(before.Dedupe);
        after.RefreshOnly.Should().BeTrue();
        (await GitLabAsync(Mr())).Single(i => i.Dedupe is not null).Dedupe.Should().Be(before.Dedupe, "an unchanged merge request is not read again");
    }

    [Fact]
    public async Task GitLab_TheNameListing_IsEveryOpenMergeRequestNewestFirst()
    {
        var urls = new List<string>();
        var http = new HttpClient(new Handler(r => { urls.Add(r.RequestUri!.Query); return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(Mr(), Encoding.UTF8, "application/json") }; }));

        await new GitLabChangedPullRequests("https://gitlab.example", "o%2Fr", "t", http).ChangedSinceAsync(Since, 10, CancellationToken.None);

        urls.Should().HaveCount(2);
        urls[0].Should().Contain("updated_after=").And.Contain("sort=asc");
        urls[1].Should().NotContain("updated_after").And.Contain("state=opened").And.Contain("sort=desc");
    }

    [Fact]
    public async Task GitLab_TheActListingIsUnchanged_AndNoRefreshNameMovesTheCursor()
    {
        var items = await GitLabAsync(Mr());

        items.Where(i => i.Dedupe is null).Should().ContainSingle().Which.RefreshOnly.Should().BeFalse();
        items.Single(i => i.Dedupe is not null).At.Should().Be(Since);
        SweepCursorPolicy.After(new SweepPosition(Since), new ChangedPage([items.Single(i => i.Dedupe is not null)], Cut: true), Since)
            .At.Should().Be(Since, "a refresh name carries the read's since");
    }

    private static async Task<string?> GitHubNameAsync(string node)
    {
        var reply = """{"data":{"repository":{"pullRequests":{"pageInfo":{"hasNextPage":false,"endCursor":null},"nodes":[""" + node + "]}}}}";
        var page = await new GitHubChangedPullRequests(GitHubGraphQlHost.Replying(reply), GitHubRepoAddress.Parse("https://github.com/o/r"))
            .ChangedSinceAsync(Since, null, 10, CancellationToken.None);
        page.Items.Should().OnlyContain(i => i.At <= Since || i.Dedupe == null);
        return page.Items.SingleOrDefault(i => i.Dedupe is not null)?.Dedupe;
    }

    private static string Pr(string review = "") =>
        """{"url":"https://github.com/o/r/pull/1","headRefName":"agent-smith/1","headRefOid":"abc","baseRefName":"main","isDraft":false,"mergeable":"MERGEABLE","isCrossRepository":false,"latestReviews":{"nodes":[""" + review + "]}}";

    private static string Original(string field) => field switch
    {
        "baseRefName" => "main", "headRefOid" => "abc", "isDraft" => "false", _ => "MERGEABLE",
    };

    private static string Field(string field, string value) => field == "isDraft" ? $"\"{field}\":{value}" : $"\"{field}\":\"{value}\"";

    private static string Mr() =>
        """[{"iid":3,"web_url":"https://gitlab.example/o/r/-/merge_requests/3","source_branch":"agent-smith/3","target_branch":"main","sha":"abc","draft":false,"detailed_merge_status":"mergeable","source_project_id":1,"target_project_id":1,"updated_at":"2026-11-25T08:59:30Z"}]""";

    private static async Task<IReadOnlyList<ChangedItem>> GitLabAsync(string body)
    {
        var http = new HttpClient(new Handler(_ => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(body, Encoding.UTF8, "application/json") }));
        return (await new GitLabChangedPullRequests("https://gitlab.example", "o%2Fr", "t", http).ChangedSinceAsync(Since, 10, CancellationToken.None)).Items;
    }

    private sealed class Handler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) => Task.FromResult(respond(request));
    }
}
