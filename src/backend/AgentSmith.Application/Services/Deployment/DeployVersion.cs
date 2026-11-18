namespace AgentSmith.Application.Services.Deployment;

/// <summary>
/// 2026-11-18-3863c: the version a deploy delivers, by the template's rule. An entry environment takes the version the
/// person named; an environment that promotes from another takes the version that one's manifest carries now, and a
/// typed version there is refused — the template decides what reaches it, not the request.
/// </summary>
public sealed class DeployVersion(DeploymentManifest manifests)
{
    /// <summary>The version, or null with the reason.</summary>
    public async Task<(string? Version, string? Error)> DecideAsync(
        DeployVersionQuestion question, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(question);
        var (declared, requested) = (question.Declared, question.Requested);
        if (declared.PromotesFrom is null)
            return requested is null ? (null, $"'{declared.Name}' is an entry environment; name the version to deliver")
                : DeployVersionShape.IsValid(requested) ? (requested, null)
                : (null, $"version '{requested}' is not deliverable: a version is {DeployVersionShape.Description}");
        return requested is not null
            ? (null, $"'{declared.Name}' promotes from '{declared.PromotesFrom}' and takes the version that one runs, "
                + "so a typed version is refused; leave it empty")
            : await PromotedAsync(question, cancellationToken);
    }

    private async Task<(string? Version, string? Error)> PromotedAsync(DeployVersionQuestion question, CancellationToken ct)
    {
        var from = question.Declared.PromotesFrom!;
        if (question.Declaration.Environment(from) is not { } source)
            return (null, $"the template says '{question.Declared.Name}' promotes from '{from}', which it does not declare");
        if (DeploymentTarget.Bound(question.Target.Binding, from) is not { } bound)
            return (null, $"'{question.Declared.Name}' promotes from '{from}', which this project does not bind, so its version cannot be read");
        var (repo, repoError) = question.Target.RepoOf(question.Project, bound);
        if (repo is null) return (null, repoError);
        if (question.Promoted is { } promoted) return await SettledAsync(question, source, repo, promoted, ct); // 3863h
        var (_, version, error) = await manifests.ReadAsync(repo, source, ct);
        if (error is not null) return (null, error);
        return DeployVersionShape.IsValid(version)
            ? (version, null)
            : (null, $"'{from}' carries version '{version}', which is not deliverable: a version is {DeployVersionShape.Description}");
    }

    // 2026-11-18-3863h: a promotion carries the version that settled green in the source environment. Where the source
    // records its version in a manifest, that manifest must still carry it — a newer delivery that has not settled is
    // not promoted; a pipeline source without a manifest is taken at the settled version.
    private async Task<(string? Version, string? Error)> SettledAsync(
        DeployVersionQuestion question, DeclaredEnvironment source, Contracts.Models.Configuration.RepoConnection repo,
        string promoted, CancellationToken ct)
    {
        if (!DeployVersionShape.IsValid(promoted))
            return (null, $"the promoted version '{promoted}' is not deliverable: a version is {DeployVersionShape.Description}");
        if (source.Manifest is null) return (promoted, null);
        var (_, current, error) = await manifests.ReadAsync(repo, source, ct);
        if (error is not null) return (null, error);
        return current == promoted ? (promoted, null)
            : (null, $"'{source.Name}' now carries '{current}', not '{promoted}' that settled there, so it is not promoted; "
                + $"deploy '{question.Declared.Name}' by hand once '{current}' settles");
    }
}
