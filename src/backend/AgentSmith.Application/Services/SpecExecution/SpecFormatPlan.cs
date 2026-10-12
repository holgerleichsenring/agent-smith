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
/// </summary>
public sealed class SpecFormatPlan
{
    public IReadOnlyList<SpecFormatInvocation> Plan(
        IReadOnlyList<RemoteContextDiscovery> declaring,
        IReadOnlyList<RemoteContextDiscovery> repositoryContexts,
        IReadOnlyList<string> changedPaths)
    {
        ArgumentNullException.ThrowIfNull(declaring);
        var workdirs = repositoryContexts.Concat(declaring)
            .Select(context => StructureWorkdir.Normalize(context.Workdir))
            .OfType<string>().Distinct(StringComparer.Ordinal).ToList();
        var files = changedPaths.Where(path => !RunRecordPaths.IsRunRecordPath(path)).ToList();
        return [.. declaring
            .Select(context => Invocation(context, workdirs, files))
            .OfType<SpecFormatInvocation>()
            .DistinctBy(invocation => invocation.CommandLine, StringComparer.Ordinal)];
    }

    private static SpecFormatInvocation? Invocation(
        RemoteContextDiscovery context, IReadOnlyList<string> workdirs, IReadOnlyList<string> files)
    {
        if (context.Format is not { } format || StructureWorkdir.Normalize(context.Workdir) is not { } own)
            return null;
        var owned = files.Where(path => StructureWorkdir.Owner(workdirs, path) == own).ToList();
        if (owned.Count == 0) return null;
        var line = format.Command.Replace(
            ContextYamlFormat.FilesPlaceholder, string.Join(' ', owned.Select(Quoted)), StringComparison.Ordinal);
        return new SpecFormatInvocation(context.ContextName, line, owned.Count);
    }

    // POSIX single quotes: nothing inside is expanded, and a quote is closed, escaped, reopened.
    // 2026-10-13-2d0c: './' first, so a path that starts with '-' never reads as an option.
    private static string Quoted(string path) => "'./" + path.Replace("'", "'\\''", StringComparison.Ordinal) + "'";
}
