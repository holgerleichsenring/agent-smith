using AgentSmith.Application.Services.Structure;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Contracts.Services;

namespace AgentSmith.Application.Services.SpecExecution;

/// <summary>
/// 2026-11-24-6d76a: what the spec commit says about the source files no formatter touched. A
/// changed file the structure engine measures, owned by no context that formatted it, counts
/// against its owner — the last owner, as in the commit gate — with the reason: the context
/// declares no format, its format's when_present path is absent, or it belongs to another sandbox
/// of the repository, whose formatter never sees this sandbox's changes. Such a file is committed
/// as written; the line makes that visible instead of silent.
/// </summary>
public sealed class SpecFormatGaps(IStructureEngine engine, StructureOwnership ownership)
{
    /// <summary>One line per context that owns unformatted source files, in context order.</summary>
    public IReadOnlyList<string> Lines(
        string key, IReadOnlyList<RemoteContextDiscovery> sandboxContexts, IReadOnlyList<RemoteContextDiscovery> declaring,
        IReadOnlyList<RemoteContextDiscovery> repositoryContexts, IReadOnlyList<string> changedPaths)
    {
        ArgumentNullException.ThrowIfNull(sandboxContexts);
        ArgumentNullException.ThrowIfNull(declaring);
        ArgumentNullException.ThrowIfNull(repositoryContexts);
        ArgumentNullException.ThrowIfNull(changedPaths);
        var contexts = sandboxContexts.Concat(repositoryContexts)
            .DistinctBy(context => context.ContextName, StringComparer.Ordinal).ToList();
        var counts = Unformatted(contexts, declaring, changedPaths);
        var own = sandboxContexts.Select(context => context.ContextName).ToHashSet(StringComparer.Ordinal);
        return [.. contexts
            .Where(context => counts.ContainsKey(context.ContextName))
            .Select(context => Line(key, context, own.Contains(context.ContextName), counts[context.ContextName]))];
    }

    private Dictionary<string, int> Unformatted(
        IReadOnlyList<RemoteContextDiscovery> contexts, IReadOnlyList<RemoteContextDiscovery> declaring,
        IReadOnlyList<string> changedPaths)
    {
        var claims = ownership.Claims(contexts);
        var formatting = declaring.Select(context => context.ContextName).ToHashSet(StringComparer.Ordinal);
        var counts = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var path in changedPaths.Where(path => !RunRecordPaths.IsRunRecordPath(path) && engine.Claims(path)))
        {
            var owners = ownership.Owners(claims, path);
            if (owners.Count == 0 || owners.Any(formatting.Contains)) continue;
            counts[owners[^1]] = counts.GetValueOrDefault(owners[^1]) + 1;
        }
        return counts;
    }

    private static string Line(string key, RemoteContextDiscovery context, bool inSandbox, int count)
    {
        var reason = !inSandbox
            ? $"{context.ContextName} files were changed in {key}, where its format does not run"
            : context.Format is { } format
                ? $"the {context.ContextName} format needs '{format.WhenPresent}', absent in {key}"
                : $"{context.ContextName} declares no format in {key}";
        return $"format: {reason}; {count} changed source file(s) are committed as written, unformatted";
    }
}
