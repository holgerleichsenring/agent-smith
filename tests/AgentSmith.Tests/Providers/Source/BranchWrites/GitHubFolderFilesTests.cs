using System.Net;
using AgentSmith.Contracts.Providers;
using AgentSmith.Domain.Models;
using AgentSmith.Infrastructure.Services.Providers.Source;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentSmith.Tests.Providers.Source.BranchWrites;

/// <summary>
/// 2026-11-23-c81ea: GitHub's folder listing reads the FOLDER'S OWN tree recursively — never the branch head's — and
/// says when the host cut it or the caller's bound was passed; the default branch is the repository's own.
/// </summary>
public sealed class GitHubFolderFilesTests
{
    private const string Parent = """
        [{"name":"poc-a","path":"deploy/poc-a","sha":"treeA","type":"dir","size":0},
         {"name":"poc-b","path":"deploy/poc-b","sha":"treeB","type":"dir","size":0}]
        """;

    private static string Tree(bool truncated) => $$"""
        {"sha":"treeA","truncated":{{(truncated ? "true" : "false")}},"tree":[
          {"path":"app.yaml","type":"blob","mode":"100644","sha":"1"},
          {"path":"sub","type":"tree","mode":"040000","sha":"2"},
          {"path":"sub/svc.yaml","type":"blob","mode":"100644","sha":"3"}]}
        """;

    private readonly GitHubHostFake _host = new();

    [Fact]
    public async Task ListFilesUnderAsync_NestedFiles_FullPathsWithoutFolders_FromTheFolderTree()
    {
        _host.On(HttpMethod.Get, "contents/deploy?ref=main", HttpStatusCode.OK, Parent)
            .On(HttpMethod.Get, "git/trees/treeA?recursive=1", HttpStatusCode.OK, Tree(truncated: false));

        var listing = await List("deploy/poc-a", maxFiles: 10);

        listing.Files.Should().Equal("deploy/poc-a/app.yaml", "deploy/poc-a/sub/svc.yaml");
        listing.Truncated.Should().BeFalse();
        _host.Requests.Should().Contain(r => r.PathAndQuery.Contains("git/trees/treeA"))
            .And.NotContain(r => r.PathAndQuery.Contains("git/trees/main"), "the branch head's tree is never read");
    }

    [Fact]
    public async Task ListFilesUnderAsync_HostTruncated_SaysTruncated()
    {
        _host.On(HttpMethod.Get, "contents/deploy?ref=main", HttpStatusCode.OK, Parent)
            .On(HttpMethod.Get, "git/trees/treeA?recursive=1", HttpStatusCode.OK, Tree(truncated: true));

        (await List("deploy/poc-a", maxFiles: 10)).Truncated.Should().BeTrue();
    }

    [Fact]
    public async Task ListFilesUnderAsync_MoreThanMaxFiles_TruncatedWithMaxFilesEntries()
    {
        _host.On(HttpMethod.Get, "contents/deploy?ref=main", HttpStatusCode.OK, Parent)
            .On(HttpMethod.Get, "git/trees/treeA?recursive=1", HttpStatusCode.OK, Tree(truncated: false));

        var listing = await List("deploy/poc-a", maxFiles: 1);

        listing.Files.Should().ContainSingle();
        listing.Truncated.Should().BeTrue();
    }

    [Fact]
    public async Task ListFilesUnderAsync_AbsentFolder_Empty()
    {
        _host.On(HttpMethod.Get, "contents/deploy?ref=main", HttpStatusCode.OK, Parent);

        (await List("deploy/poc-z", maxFiles: 10)).Files.Should().BeEmpty();
        (await List("nowhere/poc-a", maxFiles: 10)).Files.Should().BeEmpty("the parent directory answers 404");
    }

    [Fact]
    public async Task GetDefaultBranchAsync_AnswersTheRepositoryDefault()
    {
        _host.On(HttpMethod.Get, "repos/o/r", HttpStatusCode.OK, """{"id":1,"name":"r","default_branch":"trunk"}""");

        (await Provider().GetDefaultBranchAsync(CancellationToken.None)).Value.Should().Be("trunk");
    }

    private Task<RepoFileListing> List(string path, int maxFiles) =>
        Provider().ListFilesUnderAsync(path, new BranchName("main"), maxFiles, CancellationToken.None);

    private GitHubSourceProvider Provider() => new(
        new GitHubSourceConnection("https://github.com/o/r", "t", DefaultBranch: "develop"),
        _host.Factory(), NullLogger<GitHubSourceProvider>.Instance);
}
