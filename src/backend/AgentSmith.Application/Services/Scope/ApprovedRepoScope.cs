using AgentSmith.Application.Services.Specs;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Domain.Entities;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Scope;

/// <summary>
/// 2026-09-17-0e79a: the repositories an APPROVAL named, published before the inventory is
/// built — and the rule that decides when the classifier's narrowing is skipped altogether.
/// <para>
/// The seam matters: ScopeRepos reads ContextKeys.Repos into a local and builds the remote
/// context inventory from it one line later, so an approved scope published at the end would
/// inventory the wrong repositories and classify a thin ticket. The spec set is carried by the
/// FIRST repo of the resolved scope, which is why that must not happen: the carrying repo could
/// be scoped out and the next run would find no artifact.
/// </para>
/// <para>
/// An operator's <c>--repo</c> override has already narrowed ContextKeys.Repos before this runs,
/// so an approved repository the override left out is not a misconfiguration — it is the
/// operator narrowing their own approval. Under an override the approval is INTERSECTED with
/// what the operator kept and nothing fails; without one, a repository the project does not
/// configure fails the step.
/// </para>
/// <para>
/// Only the NARROWING is skipped for an approved scope. The estimate, the security refusal, the
/// per-repo expected-change gate (p0384) and the context scope (p0336b) all still run.
/// </para>
/// </summary>
public sealed class ApprovedRepoScope(
    ApprovedSpecSetResolver approvals,
    ILogger<ApprovedRepoScope> logger)
{
    /// <summary>The repositories the run works, whether an approval named them or not.</summary>
    /// <param name="FromApproval">True when a person named them, so nothing may narrow them.</param>
    /// <param name="Error">Set when the approval named a repository the project does not configure.</param>
    public sealed record Scope(
        IReadOnlyList<RepoConnection> Repos, bool FromApproval, string? Error = null);

    /// <summary>The repositories the run keeps: an approval's are never narrowed further.</summary>
    public static IReadOnlyList<RepoConnection> Kept(
        Scope scope, IReadOnlyList<RepoConnection>? scoped)
    {
        ArgumentNullException.ThrowIfNull(scope);
        return scope.FromApproval ? scope.Repos : scoped ?? scope.Repos;
    }

    /// <summary>Resolves the scope and PUBLISHES it, before anything reads ContextKeys.Repos.</summary>
    public async Task<Scope> OpenAsync(
        PipelineContext pipeline, Ticket? ticket, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(pipeline);
        var configured = pipeline.Get<IReadOnlyList<RepoConnection>>(ContextKeys.Repos);
        if (ticket is null) return new Scope(configured, false);

        var key = SpecSetKeyFactory.For(ticket, pipeline);
        var record = await approvals.ResolveAsync(pipeline, key, cancellationToken);
        if (record is null || record.Repositories.Count == 0) return new Scope(configured, false);

        var overridden = pipeline.TryGet<string>(ContextKeys.SourceOverrideRepo, out var only)
            && !string.IsNullOrWhiteSpace(only);
        var missing = record.Repositories
            .Where(name => !configured.Any(r => string.Equals(r.Name, name, StringComparison.OrdinalIgnoreCase)))
            .ToList();
        if (missing.Count > 0 && !overridden)
            return new Scope(configured, false, MissingRepos(missing, key.Value));

        var approved = configured
            .Where(r => record.Repositories.Any(n => string.Equals(n, r.Name, StringComparison.OrdinalIgnoreCase)))
            .ToList();
        // The operator narrowed to a repository the approval did not name: the flag wins, and
        // there is nothing of the approval left to publish.
        if (approved.Count == 0) return new Scope(configured, false);
        logger.LogInformation(
            "The approval of {Key} named {Count} repository/repositories: {Repos}",
            key.Value, approved.Count, string.Join(", ", approved.Select(r => r.Name)));
        pipeline.Set(ContextKeys.Repos, (IReadOnlyList<RepoConnection>)approved);
        return new Scope(approved, true);
    }

    /// <summary>Publishes what the classifier narrowed to — never over a scope a person named.</summary>
    public void Narrow(PipelineContext pipeline, Scope scope, IReadOnlyList<RepoConnection>? scoped)
    {
        ArgumentNullException.ThrowIfNull(pipeline);
        ArgumentNullException.ThrowIfNull(scope);
        if (scope.FromApproval || scoped is null) return;
        pipeline.Set(ContextKeys.Repos, scoped);
        logger.LogDebug("Repo scope narrowed to {Count} repository/repositories", scoped.Count);
    }

    /// <summary>The reason the classifier's narrowing does not apply, or null when it does.</summary>
    public static string? Settled(Scope scope)
    {
        ArgumentNullException.ThrowIfNull(scope);
        return scope.FromApproval
            ? "Repo scoping skipped: the approved specification named the repositories"
            : null;
    }

    private static string MissingRepos(IReadOnlyList<string> missing, string key) =>
        $"the approval of {key} named repository/repositories "
        + string.Join(", ", missing)
        + " which this project does not configure — the approval and the project disagree about "
        + "what the work touches, and guessing which is right is how a run edits the wrong repository";
}
