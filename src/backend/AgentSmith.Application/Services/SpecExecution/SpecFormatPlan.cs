using AgentSmith.Application.Models;
using AgentSmith.Application.Services.Structure;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Sandbox;

namespace AgentSmith.Application.Services.SpecExecution;

/// <summary>
/// 2026-10-12-fbbdb: which formatter runs over which files. A changed file belongs to the
/// context whose workdir holds it most closely — the same reading the structure gate uses — so
/// a nested context's files never reach the formatter of the context around it. Run records are
/// not code and are never formatted. Each distinct command line runs once.
/// 2026-10-13-50c7: a context also owns its sources; ownership is by context, through
/// <see cref="StructureOwnership"/>, and contexts sharing a workdir each format its files.
/// </summary>
public sealed class SpecFormatPlan(StructureOwnership ownership)
{
    public IReadOnlyList<SpecFormatInvocation> Plan(
        IReadOnlyList<RemoteContextDiscovery> declaring,
        IReadOnlyList<RemoteContextDiscovery> repositoryContexts,
        IReadOnlyList<string> changedPaths)
    {
        ArgumentNullException.ThrowIfNull(declaring);
        var claims = ownership.Claims(
            declaring.Concat(repositoryContexts).DistinctBy(context => context.ContextName, StringComparer.Ordinal));
        var files = changedPaths.Where(path => !RunRecordPaths.IsRunRecordPath(path)).ToList();
        return [.. declaring
            .Select(context => Invocation(context, claims, files))
            .OfType<SpecFormatInvocation>()
            .DistinctBy(invocation => invocation.CommandLine, StringComparer.Ordinal)];
    }

    private SpecFormatInvocation? Invocation(
        RemoteContextDiscovery context, IReadOnlyList<StructureClaim> claims, IReadOnlyList<string> files)
    {
        if (context.Format is not { } format || StructureWorkdir.Normalize(context.Workdir) is null)
            return null;
        var owned = files.Where(path => ownership.Owners(claims, path).Contains(context.ContextName)).ToList();
        if (owned.Count == 0) return null;
        var line = format.Command.Replace(
            ContextYamlFormat.FilesPlaceholder, string.Join(' ', owned.Select(Quoted)), StringComparison.Ordinal);
        return new SpecFormatInvocation(context.ContextName, line, owned.Count);
    }

    // POSIX single quotes: nothing inside is expanded, and a quote is closed, escaped, reopened.
    // 2026-10-13-2d0c: './' first, so a path that starts with '-' never reads as an option.
    private static string Quoted(string path) => "'./" + path.Replace("'", "'\\''", StringComparison.Ordinal) + "'";
}
