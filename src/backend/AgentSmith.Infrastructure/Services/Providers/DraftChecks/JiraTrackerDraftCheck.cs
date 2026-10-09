using System.Runtime.CompilerServices;
using AgentSmith.Contracts.Models.ConfigStudio;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;

namespace AgentSmith.Infrastructure.Services.Providers.DraftChecks;

/// <summary>
/// 2026-10-02-5f89b: a Jira tracker draft — "who am I" (/rest/api/3/myself, the provider's own
/// probe) with email and token, then the project when one is declared.
/// </summary>
public sealed class JiraTrackerDraftCheck(IDraftCheckHttp http) : ITrackerDraftCheck
{
    public TrackerType Type => TrackerType.Jira;

    public static string BaseOf(TrackerConnection tracker) => (tracker.Url ?? string.Empty).TrimEnd('/');

    public static DraftCheckAuth AuthOf(TrackerConnection tracker, string token) =>
        DraftCheckAuth.Basic(tracker.Email ?? string.Empty, token);

    public async IAsyncEnumerable<DraftCheckStep> RunAsync(
        TrackerConnection tracker, string token, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var auth = AuthOf(tracker, token);
        var me = await http.GetAsync($"{BaseOf(tracker)}{tracker.Endpoints.Myself}", auth, cancellationToken);
        yield return me.HostStep();
        yield return me.IdentityStep(j => j.Text("displayName") ?? j.Text("emailAddress"));

        if (string.IsNullOrWhiteSpace(tracker.Project))
        {
            yield return DraftCheckStep.Pass(DraftCheckStep.Scope, "No project declared; every project the account sees.");
            yield break;
        }
        var project = await http.GetAsync(
            $"{BaseOf(tracker)}/rest/api/3/project/{Uri.EscapeDataString(tracker.Project)}", auth, cancellationToken);
        yield return project.ScopeStep($"Project '{tracker.Project}'");
    }
}
