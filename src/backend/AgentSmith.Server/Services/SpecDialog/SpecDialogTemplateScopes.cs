using AgentSmith.Application.Services.Specs;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Sandbox;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Server.Services.SpecDialog;

/// <summary>
/// 2026-09-13-ed5a: the templates one spec-dialog TURN may read, and the provenance the
/// turn leaves behind.
/// <para>
/// The analysis is where a template decides the SLICES: a feature cut without the house
/// shape produces tickets that cross the layers the template keeps apart, and every run
/// the epic files inherits that cut. The per-ticket derivation (2026-09-13-84c0) and the
/// master that types the code (2026-09-13-6f35) both look again later; neither can undo a
/// cut already made.
/// </para>
/// <para>
/// It is its own type because <c>SpecDialogTurnRunner</c> sits at its file-length baseline,
/// and because opening a foreign checkout is a lifetime and a provenance, not two lines in
/// the method that runs the turn.
/// </para>
/// </summary>
public sealed class SpecDialogTemplateScopes(
    ProjectTemplateScopes scopes, ILogger<SpecDialogTemplateScopes> logger)
{
    /// <summary>
    /// Every template the project declares, lazily — the same shape the scope's repos take,
    /// so a turn that never opens one pays nothing and disposes to nothing. The dialog
    /// selects by no context because it has discovered none.
    /// </summary>
    public IReadOnlyDictionary<string, ISourceScopeSandbox> Open(ResolvedProject project)
    {
        ArgumentNullException.ThrowIfNull(project);
        var opened = scopes.ForProject(project);
        if (opened.Count > 0)
            logger.LogInformation(
                "The design analysis may read {Count} template(s): {Names}",
                opened.Count, string.Join(", ", opened.Keys));
        return opened;
    }

    /// <summary>
    /// Stamps what the turn had open onto the outcome it produced, so the filer — which runs
    /// after these scopes are disposed, possibly in a later process — can name it. The sha is
    /// the one the scope LANDED on; a template the analysis never opened carries the declared
    /// revision and says so, because "open" and "read at this sha" are different claims.
    /// </summary>
    public OutcomeProposal Stamp(
        OutcomeProposal outcome, ResolvedProject project,
        IReadOnlyDictionary<string, ISourceScopeSandbox> opened)
    {
        ArgumentNullException.ThrowIfNull(outcome);
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(opened);
        if (opened.Count == 0) return outcome;
        // 2026-09-16-4df5: ToDictionary RAISES on a duplicate key, and the key was the context
        // name alone — so a project declaring a template for two repositories' 'default'
        // crashed every turn here. The name now carries the local repository, and the build
        // takes the first of a genuine duplicate rather than throwing.
        var declared = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var template in project.Templates)
            declared.TryAdd(TemplateScopeName.For(template), template.Revision ?? string.Empty);
        return outcome with
        {
            Templates = [.. opened.Select(entry => Provenance(
                entry.Key, entry.Value, declared.GetValueOrDefault(entry.Key, string.Empty)))],
        };
    }

    // The sha when the scope landed on one, the declaration when it never had to look, and
    // empty when the declaration named no revision at all — the renderer says which.
    private static TemplateProvenance Provenance(
        string address, ISourceScopeSandbox scope, string declared) =>
        new(address, scope.RepoName,
            string.IsNullOrEmpty(scope.ResolvedSha) ? declared : scope.ResolvedSha!,
            scope.IsMaterialized);
}
