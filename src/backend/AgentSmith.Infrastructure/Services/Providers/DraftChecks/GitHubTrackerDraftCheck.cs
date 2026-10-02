using System.Runtime.CompilerServices;
using AgentSmith.Contracts.Models.ConfigStudio;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;

namespace AgentSmith.Infrastructure.Services.Providers.DraftChecks;

/// <summary>
/// 2026-10-02-5f89b: a GitHub issues tracker draft — who the token is, then the repository its
/// url names. The provider talks to api.github.com, so the check does too.
/// </summary>
public sealed class GitHubTrackerDraftCheck(IDraftCheckHttp http) : ITrackerDraftCheck
{
    public const string Api = "https://api.github.com";

    public TrackerType Type => TrackerType.GitHub;

    /// <summary>"owner/repo" from the tracker's repository url, or null when it names none.</summary>
    public static string? RepositoryOf(TrackerConnection tracker)
    {
        if (!Uri.TryCreate(tracker.Url, UriKind.Absolute, out var url)) return null;
        var segments = url.AbsolutePath.Trim('/').Split('/');
        return segments.Length >= 2 && segments[0].Length > 0 ? $"{segments[0]}/{segments[1]}" : null;
    }

    public async IAsyncEnumerable<DraftCheckStep> RunAsync(
        TrackerConnection tracker, string token, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var auth = DraftCheckAuth.GitHub(token);
        var me = await http.GetAsync($"{Api}/user", auth, cancellationToken);
        yield return me.HostStep();
        yield return me.IdentityStep(j => j.Text("login"));

        if (RepositoryOf(tracker) is not { } repository)
        {
            yield return DraftCheckStep.Fail(DraftCheckStep.Scope, "The tracker url names no owner/repository.");
            yield break;
        }
        yield return (await http.GetAsync($"{Api}/repos/{repository}", auth, cancellationToken))
            .ScopeStep($"Repository '{repository}'");
    }
}
