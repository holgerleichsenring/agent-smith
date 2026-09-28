using System.Net;
using System.Text;
using AgentSmith.Infrastructure.Services.Providers.Source;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentSmith.Tests.Providers;

/// <summary>
/// A pull request known only by its number has to be checked out on its head branch, so the
/// diff providers report the branch and the author the host already sends with the pull
/// request they fetch.
/// </summary>
public sealed class PrDiffHeadBranchTests
{
    [Fact]
    public async Task GitLabDiff_CarriesSourceBranchAndAuthor()
    {
        var http = Client(url => url.EndsWith("/diffs", StringComparison.Ordinal)
            ? "[]"
            : """{"diff_refs":{"base_sha":"b","head_sha":"h"},"source_branch":"feature/x","author":{"username":"dev"}}""");
        var provider = new GitLabPrDiffProvider(http, "group/repo", NullLogger<GitLabPrDiffProvider>.Instance);

        var diff = await provider.GetDiffAsync("7");

        diff.HeadBranch.Should().Be("feature/x");
        diff.Author.Should().Be("dev");
    }

    [Fact]
    public async Task AzureDevOpsDiff_CarriesTheSourceBranchWithoutItsRefPrefix()
    {
        var http = Client(url =>
            url.Contains("/changes", StringComparison.Ordinal) ? """{"changeEntries":[]}"""
            : url.Contains("/iterations", StringComparison.Ordinal) ? """{"count":1}"""
            : """{"lastMergeTargetCommit":{"commitId":"b"},"lastMergeSourceCommit":{"commitId":"h"},"sourceRefName":"refs/heads/feature/y","createdBy":{"uniqueName":"dev@example.com"}}""");
        var provider = new AzureDevOpsPrDiffProvider(
            http, "org", "proj", "repo", NullLogger<AzureDevOpsPrDiffProvider>.Instance);

        var diff = await provider.GetDiffAsync("9");

        diff.HeadBranch.Should().Be("feature/y");
        diff.Author.Should().Be("dev@example.com");
    }

    private static HttpClient Client(Func<string, string> body) =>
        new(new StubHandler(body)) { BaseAddress = new Uri("https://host.example/") };

    private sealed class StubHandler(Func<string, string> body) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(body(request.RequestUri!.ToString()), Encoding.UTF8, "application/json"),
            });
    }
}
