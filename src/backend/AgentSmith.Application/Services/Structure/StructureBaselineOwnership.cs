namespace AgentSmith.Application.Services.Structure;

/// <summary>
/// 2026-10-13-50c7: reads what a baseline round's context owns. The directories it owned before
/// are the baseline's '# owned:' line; a baseline without one owned exactly the workdir of the
/// context.yaml the round started from (the round's own workdir when there was none). A context
/// measures every file it owns or ties on, so a later round claiming the same directory finds
/// the entry in whichever baseline the gate reads.
/// </summary>
public sealed class StructureBaselineOwnership(StructureOwnership ownership, StructureBaselineOwnedLine ownedLine)
{
    /// <summary>The ownership, or null when the workdir is rooted or escapes the repository.</summary>
    public StructureBaselineOwned? For(StructureBaselineScope scope, string? existing)
    {
        ArgumentNullException.ThrowIfNull(scope);
        var own = ownership.Claims(scope.ContextName, scope.Workdir, scope.Sources);
        if (own.FirstOrDefault(c => c.IsWorkdir) is not { } workdir) return null;
        var directories = own.Select(c => c.Directory).ToList();
        var before = ownedLine.Read(existing)
            ?? [StructureWorkdir.Normalize(scope.WorkdirBefore ?? scope.Workdir) ?? workdir.Directory];
        IReadOnlyList<string> newly = existing is null ? [] : [.. directories.Except(before, StringComparer.Ordinal)];
        return new StructureBaselineOwned(scope.ContextName, directories, newly, [.. scope.Siblings, .. own]);
    }

    /// <summary>Whether the context owns the path or ties on it.</summary>
    public bool Measures(StructureBaselineOwned owned, string path) => Own(owned, path) is not null;

    /// <summary>Whether the directory the context holds the path by is one it newly owns.</summary>
    public bool IsNewlyOwned(StructureBaselineOwned owned, string path) =>
        Own(owned, path) is { } claim && owned.NewlyOwned.Contains(claim.Directory, StringComparer.Ordinal);

    private StructureClaim? Own(StructureBaselineOwned owned, string path) =>
        ownership.Claimants(owned.Claims, path).FirstOrDefault(c => c.Context == owned.Context);
}
