using AgentSmith.Contracts.Sandbox;

namespace AgentSmith.Application.Services.Structure;

/// <summary>
/// 2026-10-13-50c7: which CONTEXT a repository path belongs to, for the commit gate, the format
/// plan and the baseline alike. The longest owned directory holding the path wins, across every
/// context's workdir and sources. On equal directories a workdir beats another context's source —
/// contexts sharing one workdir all own it, as they always did — and among sources the
/// ordinal-smallest context name owns it.
/// </summary>
public sealed class StructureOwnership
{
    /// <summary>A context's claims: its normalized workdir and every source that is neither rooted,
    /// escaping, the repository root nor its workdir. A rooted or escaping workdir claims nothing.</summary>
    public IReadOnlyList<StructureClaim> Claims(string context, string? workdir, IEnumerable<string>? sources)
    {
        if (StructureWorkdir.Normalize(workdir) is not { } own) return [];
        var claims = new List<StructureClaim> { new(context, own, true) };
        foreach (var directory in (sources ?? []).Select(StructureWorkdir.Normalize).OfType<string>())
            if (directory.Length > 0 && claims.TrueForAll(c => c.Directory != directory))
                claims.Add(new StructureClaim(context, directory, false));
        return claims;
    }

    /// <summary>The claims of every given context, in their order.</summary>
    public IReadOnlyList<StructureClaim> Claims(IEnumerable<RemoteContextDiscovery> contexts) =>
        [.. contexts.SelectMany(c => Claims(c.ContextName, c.Workdir, c.Sources))];

    /// <summary>Every claim at the longest directory holding the path: the contexts that own it or tie on it.</summary>
    public IReadOnlyList<StructureClaim> Claimants(IReadOnlyList<StructureClaim> claims, string path)
    {
        var holding = claims.Where(c => StructureWorkdir.Contains(c.Directory, path)).ToList();
        if (holding.Count == 0) return [];
        var longest = holding.Max(c => c.Directory.Length);
        return [.. holding.Where(c => c.Directory.Length == longest)];
    }

    /// <summary>The contexts owning the path, in claim order: every workdir at the longest
    /// directory, else the ordinal-smallest context among the sources there; none when nothing holds it.</summary>
    public IReadOnlyList<string> Owners(IReadOnlyList<StructureClaim> claims, string path)
    {
        var claimants = Claimants(claims, path);
        var workdirs = claimants.Where(c => c.IsWorkdir).Select(c => c.Context).Distinct(StringComparer.Ordinal).ToList();
        if (workdirs.Count > 0 || claimants.Count == 0) return workdirs;
        return [claimants.Select(c => c.Context).Min(StringComparer.Ordinal)!];
    }
}
