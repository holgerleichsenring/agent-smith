using AgentSmith.Application.Models;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Contracts.Services;
using AgentSmith.Sandbox.Wire;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Handlers;

/// <summary>
/// 2026-09-23-4711: the sandbox-side half of retiring a context — the tree's context
/// directories read the way every other reader reads them, and the move for each name the
/// derivation did not produce.
/// <para>
/// A MOVE, never a delete: the tree is the only record a context ever had, and a derivation
/// that produced the wrong set once can produce a wrong set again. The moved directory sits
/// in the pull request the operator reviews, so a retirement that was wrong is one revert
/// away. The precedent is <see cref="BootstrapMetaFiles"/>, which moves a superseded FILE
/// through the same step rather than dropping it.
/// </para>
/// <para>
/// No key matches a retired context to whatever replaced it — 2026-09-23-9bb2 established
/// there is none. A name present on the tree side only needs no such match.
/// </para>
/// </summary>
public sealed class BootstrapContextRetirement(
    IProjectMetaResolver metaResolver,
    ISandboxFileReaderFactory readerFactory,
    ILogger<BootstrapContextRetirement> logger)
{
    private const int MoveTimeoutSeconds = 30;

    /// <summary>
    /// Moves every context the sandbox's tree declares that <paramref name="derived"/> does
    /// not name. Reads the tree AFTER the rounds have written, so what is retired is decided
    /// from what a round actually produced rather than from what one was asked to produce.
    /// </summary>
    public async Task<ContextRetirement> RetireAsync(
        ISandbox sandbox, IReadOnlyCollection<string> derived, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(sandbox);
        ArgumentNullException.ThrowIfNull(derived);
        var declared = await metaResolver.ResolveAllAsync(readerFactory.Create(sandbox), ct);
        var retired = new List<string>();
        var refused = new List<string>();
        foreach (var context in declared)
        {
            if (derived.Contains(context.ContextName, StringComparer.Ordinal)) continue;
            var paths = BootstrapPaths.For(context.ContextName);
            var result = await sandbox.RunStepAsync(MoveStep(paths), progress: null, ct);
            if (result.ExitCode != 0)
            {
                var reason = result.ErrorMessage ?? "unknown error";
                logger.LogWarning(
                    "{Context}: moving {From} to {To} failed — {Reason}. It stays declared.",
                    context.ContextName, paths.MetaDir, paths.RetiredMetaDir, reason);
                refused.Add($"{context.ContextName} ({reason})");
                continue;
            }
            logger.LogInformation(
                "{Context}: this derivation did not produce it — {From} moved to {To}",
                context.ContextName, paths.MetaDir, paths.RetiredMetaDir);
            retired.Add(context.ContextName);
        }
        return retired.Count == 0 && refused.Count == 0
            ? ContextRetirement.None
            : new ContextRetirement(retired, refused);
    }

    private static Step MoveStep(BootstrapPaths paths) =>
        new(Step.CurrentSchemaVersion, Guid.NewGuid(), StepKind.Run,
            Command: "sh", Args: ["-c", MoveScript(paths)], TimeoutSeconds: MoveTimeoutSeconds);

    /// <summary>
    /// <c>git mv</c> first, so the retirement reads as a rename in the pull request instead
    /// of a deletion beside an addition; an untracked directory has no index entry to move,
    /// and the plain move keeps that from failing the step. The parent is created first —
    /// the first retirement in a repository has nowhere to move to yet.
    /// </summary>
    internal static string MoveScript(BootstrapPaths paths) =>
        $"mkdir -p '{ParentOf(paths.RetiredMetaDir)}' && "
        + $"{{ git mv -- '{paths.MetaDir}' '{paths.RetiredMetaDir}' 2>/dev/null || "
        + $"mv -- '{paths.MetaDir}' '{paths.RetiredMetaDir}'; }}";

    private static string ParentOf(string path)
    {
        var idx = path.LastIndexOf('/');
        return idx <= 0 ? path : path[..idx];
    }
}
