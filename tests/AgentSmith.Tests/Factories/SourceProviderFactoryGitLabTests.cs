using AgentSmith.Infrastructure.Services.Factories;
using FluentAssertions;

namespace AgentSmith.Tests.Factories;

/// <summary>
/// p0296: the GitLab API base URL must come from the repo url's own host — a
/// self-managed instance needs no override. The repo's Host (2026-10-02-5f89a, formerly
/// GITLAB_URL) only overrides for sub-path installs.
/// </summary>
public sealed class SourceProviderFactoryGitLabTests
{
    [Fact]
    public void ResolveGitLabTarget_SelfManagedHostInUrl_UsesUrlHostNotGitlabCom()
    {
        var (baseUrl, path, clone) = SourceProviderFactory.ResolveGitLabTarget(
            "https://git.intranet.example/team/s1/area/service", null);

        baseUrl.Should().Be("https://git.intranet.example");
        path.Should().Be("team/s1/area/service");
        clone.Should().Be("https://git.intranet.example/team/s1/area/service.git");
    }

    [Fact]
    public void ResolveGitLabTarget_GitlabDotComUrl_UsesGitlabCom()
    {
        var (baseUrl, _, _) = SourceProviderFactory.ResolveGitLabTarget(
            "https://gitlab.com/acme/repo", null);

        baseUrl.Should().Be("https://gitlab.com");
    }

    [Fact]
    public void ResolveGitLabTarget_WithOverride_UsesOverrideBase()
    {
        var (baseUrl, _, clone) = SourceProviderFactory.ResolveGitLabTarget(
            "https://git.intranet.example/group/repo", "https://mirror.example/gitlab/");

        baseUrl.Should().Be("https://mirror.example/gitlab");
        clone.Should().Be("https://mirror.example/gitlab/group/repo.git");
    }
}
