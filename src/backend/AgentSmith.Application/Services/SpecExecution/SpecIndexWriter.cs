using AgentSmith.Application.Services.Handlers;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Contracts.Services;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.SpecExecution;

/// <summary>
/// 2026-08-26-31e5: writes the index line for a finished spec into the done index of the context the change was made
/// in.
/// <para>
/// The SANDBOX routes it, not the spec's <c>applies_to</c>: that key is in no schema, is set by nothing in the
/// product, and where present names an AREA ("backend") rather than a context. The change was made in a sandbox, and
/// <see cref="SandboxTargets"/> already resolves that sandbox's context.
/// </para>
/// <para>
/// 2026-10-06-03c7e: written into the CARRYING repository only, and the paths it wrote are returned so the record
/// commit carries the line beside the done file it names.
/// </para>
/// <para>
/// 2026-11-26-462aa: the index is the context's done.md, a line file two branches' lines union-merge in. A repository
/// whose context.yaml files still carry state.done is migrated by this write (<see cref="ContextDoneMigration"/>), and
/// its .gitattributes gets the union lines (<see cref="IndexMergeAttributes"/>).
/// </para>
/// </summary>
public sealed class SpecIndexWriter(
    ISandboxFileReaderFactory readerFactory,
    ContextDoneMigration migration,
    SandboxTargets sandboxTargets,
    ILogger<SpecIndexWriter> logger)
{
    /// <summary>The repo-relative paths it changed — the context's done.md first — or none when the line reached no
    /// context: a pointer without one is the defect.</summary>
    public async Task<IReadOnlyList<string>> WriteAsync(
        PipelineContext pipeline, ISandbox sandbox, string? sandboxKey,
        string repoLocalPath, string specId, string line, CancellationToken ct)
    {
        var discovery = Discovery(pipeline, sandbox, sandboxKey);
        if (discovery is null)
        {
            logger.LogWarning(
                "SpecIndexWriter: no discovered context for sandbox '{Key}' — no index line",
                sandboxKey ?? "(single)");
            return [];
        }

        var reader = readerFactory.Create(sandbox);
        var plan = await migration.PlanAsync(reader, repoLocalPath, ct);
        var relative = ContextDoneMigration.DonePath(discovery.ContextName);
        var path = Path.Combine(repoLocalPath, relative);
        var existing = plan.Done.TryGetValue(discovery.ContextName, out var moved) ? moved : await reader.TryReadAsync(path, ct);
        await reader.WriteAsync(path, ContextDoneIndex.Upsert(existing, specId, line), ct);

        var changed = new List<string> { relative };
        var files = plan.Done.Where(d => d.Key != discovery.ContextName)
            .Select(d => (ContextDoneMigration.DonePath(d.Key), d.Value))
            .Concat(plan.Yaml.Select(y => (ContextDoneMigration.YamlPath(y.Key), y.Value)));
        var attributes = Path.Combine(repoLocalPath, ProjectMetaPaths.GitAttributes);
        if (IndexMergeAttributes.Ensure(await reader.TryReadAsync(attributes, ct)) is { } ensured)
            files = files.Append((ProjectMetaPaths.GitAttributes, ensured));
        foreach (var (file, text) in files)
        {
            await reader.WriteAsync(Path.Combine(repoLocalPath, file), text, ct);
            changed.Add(file);
        }
        logger.LogInformation("Spec index line written to {Path}", path);
        return changed;
    }

    private RemoteContextDiscovery? Discovery(
        PipelineContext pipeline, ISandbox sandbox, string? sandboxKey)
    {
        if (!sandboxTargets.TryResolve(pipeline, out var sandboxes, out var discoveries))
            return null;
        var key = sandboxKey
            ?? sandboxes.FirstOrDefault(pair => ReferenceEquals(pair.Value, sandbox)).Key;
        if (key is not null && discoveries.TryGetValue(key, out var discovery)) return discovery;
        return discoveries.Count == 1 ? discoveries.Values.First() : null;
    }
}
