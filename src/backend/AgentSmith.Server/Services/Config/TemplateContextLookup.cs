using System.Text.RegularExpressions;
using AgentSmith.Application.Services.Sandbox;
using AgentSmith.Contracts.Models.ConfigStudio;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;

namespace AgentSmith.Server.Services.Config;

/// <summary>
/// 2026-09-14-620e: what context names one repository of one project declares, so the
/// studio's template form can offer them instead of asking an operator to remember them.
/// <para>
/// It reads through <see cref="ISandboxLanguageResolver.ListContextsAsync"/>, which is a
/// provider call over <c>.agentsmith/contexts</c> and needs no sandbox and no run —
/// ConnectionDiagnosticsService already makes provider calls from a plain server service.
/// </para>
/// </summary>
public sealed partial class TemplateContextLookup(
    IConfigurationLoader loader,
    ISandboxLanguageResolver contexts)
{
    /// <summary>Longest reason served; a provider stack trace is not an explanation.</summary>
    private const int ReasonBound = 300;

    /// <summary>
    /// The contexts of <paramref name="repoRef"/> as declared by <paramref name="project"/>,
    /// or null when either name resolves to nothing — which is a 404, not an empty list:
    /// "this project has no such repo" and "that repo declares no contexts" are opposite
    /// instructions to whoever is filling the form in.
    /// </summary>
    public async Task<ProjectContextsView?> ListAsync(
        string project, string repoRef, CancellationToken cancellationToken)
    {
        // The AgentSmithConfig singleton is built once at composition and the config-epoch
        // reloader only reloads the STORE, so a repo added in the studio would be invisible
        // here until a restart — in exactly the session that added it. The loader
        // re-assembles from the document store, and it is also the only path that turns a
        // repo ref into a RepoConnection: RepoEntity carries id/name/branch and no type.
        var config = loader.LoadConfig(string.Empty);
        var resolved = config.Projects
            .FirstOrDefault(p => ConfigNames.AreSame(p.Key, project)).Value;
        if (resolved is null) return null;

        var repo = resolved.Repos.FirstOrDefault(r => Names(repoRef).Any(n => ConfigNames.AreSame(r.Name, n)));
        if (repo is null) return null;

        var listing = await contexts.ListContextsAsync(repo, cancellationToken);
        return listing.IsUnreadable
            ? new ProjectContextsView([], Sanitised(listing.UnreadableReason!))
            : new ProjectContextsView([.. listing.Contexts.Select(c => c.ContextName)]);
    }

    /// <summary>
    /// What a resolved repo's <see cref="RepoConnection.Name"/> may be for one declared ref:
    /// a plain catalog ref keeps its name, while a connection-scoped ref
    /// (<c>conn/Sample.Api</c>) resolves to the REPO name alone — ConnectionRepoUrlBuilder
    /// sets Name to the repo, never to the ref that named it.
    /// </summary>
    private static IEnumerable<string> Names(string repoRef)
    {
        yield return repoRef;
        var slash = repoRef.LastIndexOf('/');
        if (slash >= 0 && slash < repoRef.Length - 1) yield return repoRef[(slash + 1)..];
    }

    /// <summary>
    /// A provider's own exception text, made servable. It is third-party text that can carry
    /// a URL, and a URL can carry userinfo — on a redirect, the credential the call was made
    /// with. The userinfo is dropped and the whole thing is bounded.
    /// </summary>
    private static string Sanitised(string reason)
    {
        var stripped = UserInfo().Replace(reason, "$1");
        return stripped.Length <= ReasonBound ? stripped : stripped[..ReasonBound] + "…";
    }

    [GeneratedRegex(@"([a-zA-Z][a-zA-Z0-9+.-]*://)[^/\s@]+@", RegexOptions.None, 200)]
    private static partial Regex UserInfo();
}
