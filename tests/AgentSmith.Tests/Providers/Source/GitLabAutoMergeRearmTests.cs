using System.Net;
using System.Text;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Reviews;
using AgentSmith.Infrastructure.Services.Providers.Source;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentSmith.Tests.Providers.Source;

/// <summary>
/// 2026-11-25-7a92c: GitLab says whether auto-merge is set (merge_when_pipeline_succeeds, per docs.gitlab.com), and a dropped one
/// is armed again through the first arm's PUT .../merge with auto_merge; one armed already, a fork's and an ended one are not.
/// </summary>
public sealed class GitLabAutoMergeRearmTests
{
    private const string Base = "https://gitlab.example.com";
    private const string Url = "https://gitlab.example.com/group/repo/-/merge_requests/7";
    private readonly List<string> _puts = [];

    [Theory]
    [InlineData("true", true)]
    [InlineData("false", false)]
    public async Task StateRead_SaysWhetherAutoMergeIsSet(string flag, bool armed)
    {
        var state = await StateAsync(Mr("opened", flag));

        state.AutoCompleteArmed.Should().Be(armed);
    }

    [Fact]
    public async Task StateRead_AnEndedMergeRequest_SaysNothing() =>
        (await StateAsync(Mr("merged", "false"))).AutoCompleteArmed.Should().BeNull();

    [Fact]
    public async Task RearmAsync_ADroppedOne_IsArmedThroughTheFirstArmsRequest()
    {
        var answer = await RearmAsync(Mr("opened", "false"));

        answer.Outcome.Should().Be(PullRequestCompletionOutcome.Armed);
        _puts.Should().ContainSingle().Which.Should().Contain("\"auto_merge\":true");
    }

    [Theory]
    [InlineData("opened", "true", 1, PullRequestCompletionOutcome.Armed)]
    [InlineData("opened", "false", 99, PullRequestCompletionOutcome.Refused)]
    [InlineData("merged", "false", 1, PullRequestCompletionOutcome.Refused)]
    [InlineData("opened", "false", 0, PullRequestCompletionOutcome.Refused)] // no project ids answered: not known to be no fork
    public async Task RearmAsync_ArmedAForkOrEnded_PutsNothing(string state, string flag, int source, PullRequestCompletionOutcome outcome)
    {
        (await RearmAsync(Mr(state, flag, source))).Outcome.Should().Be(outcome);

        _puts.Should().BeEmpty();
    }

    private static string Mr(string state, string armed, int source = 1) =>
        $$"""{"iid":7,"state":"{{state}}","sha":"abc","created_at":"2026-11-25T08:00:00Z","author":{"id":2},{{Projects(source)}}"merge_when_pipeline_succeeds":{{armed}}}""";

    private static string Projects(int source) => source == 0 ? string.Empty : $"\"source_project_id\":{source},\"target_project_id\":1,";

    private Task<PullRequestCompletion> RearmAsync(string mr)
    {
        var http = Http(mr);
        var updater = new GitLabMergeRequestUpdater(Base, "group%2Frepo", "glpat-test", http, NullLogger.Instance);
        return new GitLabAutoMergeRearm(new GitLabMergeRequestRead(Base, "group%2Frepo", "glpat-test", http),
            new GitLabMergeModes(Base, "group%2Frepo", "glpat-test", http, updater, NullLogger.Instance)).RearmAsync(Url, CancellationToken.None);
    }

    private Task<PrReviewState> StateAsync(string mr) =>
        new GitLabMrStateReader(Base, "group%2Frepo", "glpat-test", "https://gitlab.example.com/group/repo", Http(mr), NullLogger.Instance)
            .ReadAsync(Url, CancellationToken.None);

    private HttpClient Http(string mr) => new(new Handler(async request =>
    {
        var url = request.RequestUri!.AbsolutePath;
        if (request.Method == HttpMethod.Put)
        {
            _puts.Add(await request.Content!.ReadAsStringAsync());
            return Json("""{"iid":7,"state":"opened","merge_when_pipeline_succeeds":true}""");
        }
        return url.EndsWith("/merge_requests/7", StringComparison.Ordinal) ? Json(mr)
            : url.EndsWith("/user", StringComparison.Ordinal) ? Json("""{"id":1}""")
            : url.EndsWith("/notes", StringComparison.Ordinal) ? Json("[]") : new HttpResponseMessage(HttpStatusCode.NotFound);
    }));

    private static HttpResponseMessage Json(string body) => new(HttpStatusCode.OK) { Content = new StringContent(body, Encoding.UTF8, "application/json") };

    private sealed class Handler(Func<HttpRequestMessage, Task<HttpResponseMessage>> respond) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) => respond(request);
    }
}
