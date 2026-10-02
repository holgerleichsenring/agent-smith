using AgentSmith.Contracts.Models.ConfigStudio;
using AgentSmith.Contracts.Services;
using FluentAssertions;
using static AgentSmith.Tests.ConfigStudio.DraftChecks.DraftCheckFixtures;

namespace AgentSmith.Tests.ConfigStudio.DraftChecks;

/// <summary>2026-10-02-5f89b: an unsaved tracker checked step by step, then its open tickets counted.</summary>
public sealed class TrackerDraftCheckTests
{
    private const string Jira = "https://board.example.test";

    private static TrackerEntity JiraDraft(string? email = "bot@example.test") =>
        new("jira", "jira", "jira_a", Url: Jira, Project: "OPS", Email: email);

    [Fact]
    public async Task TrackerDraftCheck_Jira_ReportsTheAccountAndABoundedOpenTicketCount()
    {
        var host = new ScriptedHostHandler()
            .On($"{Jira}/rest/api/3/myself", 200, """{"displayName":"Ops Bot"}""")
            .On($"{Jira}/rest/api/3/project/OPS", 200, """{"key":"OPS"}""")
            .On($"{Jira}/rest/api/3/search/jql", 200, $$"""{"issues":{{Items(100)}},"isLast":false}""");

        var report = await Trackers(host).RunAsync(JiraDraft(), CancellationToken.None);

        Keys(report).Should().Be("secret:ok,host:ok,identity:ok,scope:ok,tickets:ok");
        report.Steps[2].Detail.Should().Be("Signed in as Ops Bot.");
        report.Steps[^1].Detail.Should().Be("At least 100 open tickets.");
        host.Requests[^1].Body.Should().Contain("project = \\u0022OPS\\u0022 AND statusCategory != Done");
        host.Requests.Should().OnlyContain(r => r.Auth.StartsWith("Basic "));
    }

    [Fact]
    public async Task TrackerDraftCheck_JiraWithoutEmail_StopsAtTheSecretStep()
    {
        var report = await Trackers(new ScriptedHostHandler()).RunAsync(JiraDraft(email: null), CancellationToken.None);

        Keys(report).Should().Be("secret:fail");
        report.Steps[0].Detail.Should().Contain("declares no email");
    }

    [Fact]
    public async Task TrackerDraftCheck_ProviderWithoutListing_SaysCountNotSupported()
    {
        var host = new ScriptedHostHandler()
            .On($"{Jira}/rest/api/3/myself", 200, """{"displayName":"Ops Bot"}""")
            .On($"{Jira}/rest/api/3/project/OPS", 200, """{"key":"OPS"}""");

        var report = await Trackers(host, counts: []).RunAsync(JiraDraft(), CancellationToken.None);

        report.Steps[^1].Should().Be(new DraftCheckStep("tickets", "Open tickets", true, "Count not supported by this tracker."));
        host.Requests.Should().HaveCount(2);
    }

    [Fact]
    public async Task TrackerDraftCheck_GitHub_CountsIssuesButNotPullRequests()
    {
        var host = new ScriptedHostHandler()
            .On("https://api.github.com/user", 200, """{"login":"octo"}""")
            .On("https://api.github.com/repos/octo/app", 200, """{"full_name":"octo/app"}""")
            .On("https://api.github.com/repos/octo/app/issues", 200,
                """[{"id":1},{"id":2,"pull_request":{}},{"id":3}]""");
        var draft = new TrackerEntity("gh", "github", "gitlab_a", Url: "https://github.com/octo/app");

        var report = await Trackers(host).RunAsync(draft, CancellationToken.None);

        Keys(report).Should().Be("secret:ok,host:ok,identity:ok,scope:ok,tickets:ok");
        report.Steps[^1].Detail.Should().Be("2 open tickets.");
    }

    [Fact]
    public async Task TrackerDraftCheck_GitLab_ReadsItsOwnUrlAndProjectPath()
    {
        var api = "https://issues.example.test/api/v4";
        var host = new ScriptedHostHandler()
            .On($"{api}/user", 200, """{"username":"bot"}""")
            .On($"{api}/projects/team%2Fapp", 200, """{"id":7}""")
            .On($"{api}/projects/team%2Fapp/issues", 200, Items(4));
        var draft = new TrackerEntity("gl", "gitlab", "gitlab_a", Url: "https://issues.example.test/", Project: "team/app");

        var report = await Trackers(host).RunAsync(draft, CancellationToken.None);

        Keys(report).Should().Be("secret:ok,host:ok,identity:ok,scope:ok,tickets:ok");
        report.Steps[^1].Detail.Should().Be("4 open tickets.");
    }

    [Fact]
    public async Task TrackerDraftCheck_AzureDevOps_CountsOpenWorkItemsThroughWiql()
    {
        var host = new ScriptedHostHandler()
            .On("https://dev.azure.com/org/_apis/connectionData", 200,
                """{"authenticatedUser":{"providerDisplayName":"Bot"}}""")
            .On("https://dev.azure.com/org/_apis/projects/proj", 200, """{"name":"proj"}""")
            .On("https://dev.azure.com/org/proj/_apis/wit/wiql", 200, """{"workItems":[{"id":1}]}""");
        var draft = new TrackerEntity("ado", "azure_devops", "gitlab_a", Organization: "org", Project: "proj",
            OpenStates: ["Doing"]);

        var report = await Trackers(host).RunAsync(draft, CancellationToken.None);

        Keys(report).Should().Be("secret:ok,host:ok,identity:ok,scope:ok,tickets:ok");
        report.Steps[^1].Detail.Should().Be("1 open tickets.");
        host.Requests[^1].Body.Should().Contain("[System.State] IN (\\u0027Doing\\u0027)");
    }
}
