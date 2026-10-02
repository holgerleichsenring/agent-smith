using System.Text.Json;
using AgentSmith.Contracts.Models.ConfigStudio;
using FluentAssertions;
using static AgentSmith.Tests.ConfigStudio.DraftChecks.DraftCheckFixtures;

namespace AgentSmith.Tests.ConfigStudio.DraftChecks;

/// <summary>2026-10-02-5f89b: an unsaved connection checked step by step, stopping at the first failure.</summary>
public sealed class ConnectionDraftCheckTests
{
    private const string Api = "https://git.example.test/api/v4";

    [Fact]
    public async Task ConnectionDraftCheck_GitLab_AllGood_ShowsHostIdentityGroupAndRepoCount()
    {
        var host = new ScriptedHostHandler()
            .On($"{Api}/user", 200, """{"username":"bot"}""")
            .On($"{Api}/groups/acme", 200, """{"full_path":"acme"}""")
            .On($"{Api}/groups/acme/projects", 200, Items(3));

        var report = await Connections(host).RunAsync(GitLab(), CancellationToken.None);

        Keys(report).Should().Be("secret:ok,host:ok,identity:ok,scope:ok,repos:ok");
        report.Steps[2].Detail.Should().Be("Signed in as bot.");
        report.Steps[4].Detail.Should().Be("3 repositories visible.");
        host.Requests.Should().OnlyContain(r => r.Auth == Token);
        host.Requests[2].Url.Should().Contain("include_subgroups=true&with_shared=false");
    }

    [Fact]
    public async Task ConnectionDraftCheck_GitLab_WrongToken_StopsAtIdentityWith401()
    {
        var host = new ScriptedHostHandler().On($"{Api}/user", 401, """{"message":"401 Unauthorized"}""");

        var report = await Connections(host).RunAsync(GitLab(), CancellationToken.None);

        Keys(report).Should().Be("secret:ok,host:ok,identity:fail");
        report.Steps[^1].Detail.Should().Contain("HTTP 401");
        host.Requests.Should().ContainSingle();
    }

    [Fact]
    public async Task ConnectionDraftCheck_GitLab_UnknownGroup_StopsAtScope()
    {
        var host = new ScriptedHostHandler()
            .On($"{Api}/user", 200, """{"username":"bot"}""")
            .On($"{Api}/groups/acme", 404, """{"message":"404 Group Not Found"}""");

        var report = await Connections(host).RunAsync(GitLab(), CancellationToken.None);

        Keys(report).Should().Be("secret:ok,host:ok,identity:ok,scope:fail");
        report.Steps[^1].Detail.Should().Contain("Group 'acme' was not found").And.NotContain("Group Not Found");
        host.Requests.Should().HaveCount(2);
    }

    [Fact]
    public async Task ConnectionDraftCheck_HostAnswersARedirect_FailsHostAndNeverFollows()
    {
        var host = new ScriptedHostHandler().On($"{Api}/user", 302, string.Empty, "https://elsewhere.example.test/");

        var report = await Connections(host).RunAsync(GitLab(), CancellationToken.None);

        Keys(report).Should().Be("secret:ok,host:fail");
        report.Steps[^1].Detail.Should().Contain("HTTP 302").And.Contain("does not follow redirects");
        host.Requests.Should().ContainSingle().Which.Url.Should().StartWith(Api);
    }

    [Fact]
    public async Task ConnectionDraftCheck_UnresolvableHost_FailsTheHostStep()
    {
        var host = new ScriptedHostHandler().Throws(Api, HttpRequestError.NameResolutionError);

        var report = await Connections(host).RunAsync(GitLab(), CancellationToken.None);

        Keys(report).Should().Be("secret:ok,host:fail");
        report.Steps[^1].Detail.Should().Contain("does not resolve");
    }

    [Fact]
    public async Task ConnectionDraftCheck_AzureDevOps_SignInPageAnswer_IsAnIdentityFailure()
    {
        var host = new ScriptedHostHandler()
            .On("https://dev.azure.com/org/_apis/connectionData", 203, "<html><body>Sign in</body></html>");
        var draft = new ConnectionEntity("ado", "azure_devops", "org", "proj", "gitlab_a", null);

        var report = await Connections(host).RunAsync(draft, CancellationToken.None);

        Keys(report).Should().Be("secret:ok,host:ok,identity:fail");
        report.Steps[^1].Detail.Should().Contain("not JSON").And.NotContain("Sign in");
        host.Requests.Single().Auth.Should().StartWith("Basic ");
    }

    [Fact]
    public async Task ConnectionDraftCheck_AzureDevOps_AllGood_CountsTheProjectsRepositories()
    {
        var host = new ScriptedHostHandler()
            .On("https://dev.azure.com/org/_apis/connectionData", 200,
                """{"authenticatedUser":{"providerDisplayName":"Build Bot"}}""")
            .On("https://dev.azure.com/org/_apis/projects/proj", 200, """{"name":"proj"}""")
            .On("https://dev.azure.com/org/proj/_apis/git/repositories", 200, """{"value":[{"name":"a"},{"name":"b"}]}""");
        var draft = new ConnectionEntity("ado", "azure_devops", "org", "proj", "gitlab_a", null);

        var report = await Connections(host).RunAsync(draft, CancellationToken.None);

        Keys(report).Should().Be("secret:ok,host:ok,identity:ok,scope:ok,repos:ok");
        report.Steps[2].Detail.Should().Be("Signed in as Build Bot.");
        report.Steps[^1].Detail.Should().Be("2 repositories visible.");
    }

    [Fact]
    public async Task ConnectionDraftCheck_GitHub_OwnerIsAUser_FallsBackAndCountsAFullPageAsAtLeast()
    {
        var host = new ScriptedHostHandler()
            .On("https://api.github.com/user", 200, """{"login":"octo"}""")
            .On("https://api.github.com/users/octo", 200, """{"login":"octo"}""")
            .On("https://api.github.com/users/octo/repos", 200, Items(100));
        var draft = new ConnectionEntity("gh", "github", "octo", null, "gitlab_a", null);

        var report = await Connections(host).RunAsync(draft, CancellationToken.None);

        Keys(report).Should().Be("secret:ok,host:ok,identity:ok,scope:ok,repos:ok");
        report.Steps[^1].Detail.Should().Be("At least 100 repositories visible.");
        host.Requests.Select(r => r.Url).Should().Contain("https://api.github.com/orgs/octo");
    }

    [Fact]
    public async Task ConnectionDraftCheck_SecretWithoutValue_StopsAtSecretNamingItNotAValue()
    {
        var host = new ScriptedHostHandler();

        var report = await Connections(host).RunAsync(GitLab(auth: "gitlab_missing"), CancellationToken.None);

        Keys(report).Should().Be("secret:fail");
        report.Steps[0].Detail.Should().Contain("'gitlab_missing'").And.NotContain(Token);
        host.Requests.Should().BeEmpty();
    }

    [Fact]
    public async Task DraftCheck_Report_NeverContainsTheTokenValue()
    {
        var host = new ScriptedHostHandler()
            .On($"{Api}/user", 200, $$"""{"username":"bot","echo":"{{Token}}"}""")
            .On($"{Api}/groups/acme", 403, $$"""{"message":"token {{Token}} lacks read_api"}""");

        var report = await Connections(host).RunAsync(GitLab(), CancellationToken.None);

        Keys(report).Should().Be("secret:ok,host:ok,identity:ok,scope:fail");
        JsonSerializer.Serialize(report).Should().NotContain(Token).And.NotContain("read_api");
    }
}
