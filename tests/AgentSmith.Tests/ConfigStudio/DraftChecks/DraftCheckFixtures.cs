using AgentSmith.Contracts.Models.ConfigStudio;
using AgentSmith.Contracts.Services;
using AgentSmith.Infrastructure.Services.Providers.DraftChecks;
using AgentSmith.Tests.TestSupport;

namespace AgentSmith.Tests.ConfigStudio.DraftChecks;

/// <summary>2026-10-02-5f89b: runners over a scripted host and the real credential resolver.</summary>
internal static class DraftCheckFixtures
{
    public const string Token = "glpat-very-secret-token";

    public static ICredentialResolver Credentials() => TestCredentials.With(("gitlab_a", Token), ("jira_a", Token));

    public static ConnectionDraftCheckRunner Connections(ScriptedHostHandler host)
    {
        var http = host.Http();
        return new ConnectionDraftCheckRunner(Credentials(),
            [new GitLabConnectionDraftCheck(http), new GitHubConnectionDraftCheck(http), new AzureDevOpsConnectionDraftCheck(http, new AzureDevOpsDraftSteps(http))],
            new DraftCheckCollector());
    }

    public static TrackerDraftCheckRunner Trackers(
        ScriptedHostHandler host, IEnumerable<ITicketCountCapability>? counts = null)
    {
        var http = host.Http();
        return new TrackerDraftCheckRunner(Credentials(),
            [new GitLabTrackerDraftCheck(http), new GitHubTrackerDraftCheck(http),
             new AzureDevOpsTrackerDraftCheck(new AzureDevOpsDraftSteps(http)), new JiraTrackerDraftCheck(http)],
            counts ?? [new GitLabTicketCount(http), new GitHubTicketCount(http),
                       new AzureDevOpsTicketCount(http), new JiraTicketCount(http)],
            new DraftCheckCollector());
    }

    public static ConnectionEntity GitLab(string auth = "gitlab_a") =>
        new("gl", "gitlab", "acme", null, auth, null, "https://git.example.test");

    public static string Keys(DraftCheckReport report) =>
        string.Join(",", report.Steps.Select(s => $"{s.Key}:{(s.Ok ? "ok" : "fail")}"));

    public static string Items(int count, string extra = "") =>
        "[" + string.Join(",", Enumerable.Range(0, count).Select(i => $"{{\"id\":{i}{extra}}}")) + "]";
}
