using AgentSmith.Infrastructure.Core.Services.Configuration;
using AgentSmith.Infrastructure.Services.Providers.Discovery;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace AgentSmith.Tests.Providers.Discovery;

/// <summary>
/// 2026-08-26-5c85: GitLab discovery lists subgroups, so the bare project slug is
/// not unique — a discovered repo is named by its path RELATIVE to the connection's
/// group, and only falls back to the slug (with a warning) when the group prefix
/// cannot be stripped. Parse is the test seam: the provider's static HttpClient
/// leaves DiscoverAsync without an HTTP seam.
/// </summary>
public sealed class GitLabRepoDiscoveryProviderParseTests
{
    [Fact]
    public void Parse_ProjectInSubgroup_NameIsNamespaceRelative()
    {
        var repos = NewProvider().Parse(
            Projects(Project("api", "root/team-a/api")), "root");

        var repo = repos.Should().ContainSingle().Subject;
        repo.Name.Should().Be("team-a/api");
        repo.Url.Should().Be("https://gitlab.example/root/team-a/api.git");
        repo.DefaultBranch.Should().Be("main");
    }

    [Fact]
    public void Parse_TopLevelProject_NameStaysBareSlug()
    {
        var repos = NewProvider().Parse(
            Projects(Project("api", "root/api")), "root");

        repos.Should().ContainSingle().Which.Name.Should().Be("api");
    }

    [Fact]
    public void Parse_SameSlugInTwoSubgroups_YieldsTwoDistinctNames()
    {
        var repos = NewProvider().Parse(
            Projects(
                Project("api", "root/team-a/api"),
                Project("api", "root/team-b/api")),
            "root");

        repos.Select(r => r.Name).Should().BeEquivalentTo("team-a/api", "team-b/api");
    }

    [Fact]
    public void Parse_NestedConnectionGroup_StripsTheFullGroupPath()
    {
        var repos = NewProvider().Parse(
            Projects(Project("api", "root/platform/api")), "root/platform");

        repos.Should().ContainSingle().Which.Name.Should().Be("api");
    }

    [Fact]
    public void Parse_PathWithNamespaceMissing_FallsBackToBarePath()
    {
        var repos = NewProvider().Parse(
            Projects(Project("api", pathWithNamespace: null)), "root");

        repos.Should().ContainSingle().Which.Name.Should().Be("api");
    }

    [Fact]
    public void Parse_GroupPrefixCaseDiffers_StillStripsPrefix()
    {
        var repos = NewProvider().Parse(
            Projects(Project("api", "Root/Team-A/api")), "root");

        repos.Should().ContainSingle().Which.Name.Should().Be("Team-A/api");
    }

    [Fact]
    public void Parse_ForeignNamespace_FallsBackToBarePathAndWarns()
    {
        var logger = new CapturingLogger();

        var repos = NewProvider(logger).Parse(
            Projects(Project("api", "elsewhere/api")), "root");

        repos.Should().ContainSingle().Which.Name.Should().Be("api");
        logger.Warnings.Should().ContainSingle()
            .Which.Should().Contain("elsewhere/api").And.Contain("root");
    }

    private static GitLabRepoDiscoveryProvider NewProvider(ILogger<GitLabRepoDiscoveryProvider>? logger = null) =>
        new(new SecretsProvider(), logger ?? NullLogger<GitLabRepoDiscoveryProvider>.Instance);

    private static string Projects(params string[] projects) => "[" + string.Join(",", projects) + "]";

    private static string Project(string path, string? pathWithNamespace)
    {
        var namespaceField = pathWithNamespace is null
            ? string.Empty
            : $"\"path_with_namespace\": \"{pathWithNamespace}\",";
        var urlPath = pathWithNamespace ?? path;
        return $$"""
            {
              "path": "{{path}}",
              {{namespaceField}}
              "http_url_to_repo": "https://gitlab.example/{{urlPath}}.git",
              "default_branch": "main"
            }
            """;
    }

    private sealed class CapturingLogger : ILogger<GitLabRepoDiscoveryProvider>
    {
        public List<string> Warnings { get; } = new();

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            if (logLevel == LogLevel.Warning) Warnings.Add(formatter(state, exception));
        }
    }
}
