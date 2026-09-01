using AgentSmith.Application.Models;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Sandbox.Wire;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Handlers;

/// <summary>
/// The sandbox-side view of one context's meta files: what a bootstrap round finds already
/// written, and whether a file it wrote is really there afterwards.
/// <para>
/// 2026-09-01-72c5: it also performs the one migration that has to happen BEFORE the round
/// looks. A repository initialised before the rename carries only the retired principles
/// name, while the preserve path reads the current one — so it reported nothing existing and
/// composed a fresh file over the operator's ratified section. Renaming first leaves exactly
/// one downstream path. Extracted from <see cref="BootstrapRoundHandler"/>, which is over the
/// length its ratchet allows.
/// </para>
/// </summary>
public sealed class BootstrapMetaFiles(
    ISandboxFileReaderFactory readerFactory, ILogger<BootstrapMetaFiles> logger)
{
    private const int MoveTimeoutSeconds = 30;

    // p0202d: the operator's existing context.yaml + principles.md, so the producer
    // merges (preserve + backfill) instead of regenerating from source and clobbering.
    // Both null on cold-init → generate-from-scratch.
    public async Task<ExistingMetaFiles> ReadAsync(
        ISandbox sandbox, string contextName, CancellationToken ct)
    {
        var (renamed, error) = await MoveRetiredPrinciplesAsync(sandbox, contextName, ct);
        if (error is not null) return new ExistingMetaFiles(null, null, false, error);
        var (ctxPath, principlesPath) = BootstrapPromptFactory.ResolveTargetPaths(contextName);
        var reader = readerFactory.Create(sandbox);
        return new ExistingMetaFiles(
            await reader.TryReadAsync(ctxPath, ct),
            await reader.TryReadAsync(principlesPath, ct),
            renamed);
    }

    /// <summary>Whether the named file carries content on the sandbox right now.</summary>
    public async Task<bool> ExistsAsync(ISandbox sandbox, string path, CancellationToken ct)
    {
        var content = await readerFactory.Create(sandbox).TryReadAsync(path, ct);
        return !string.IsNullOrEmpty(content);
    }

    /// <summary>
    /// 2026-09-01-72c5: a MOVE, so no repository is left carrying both names for the coding
    /// agent to choose between. Both names present skips it entirely — whatever produced the
    /// current file is more recent than what stopped being written.
    /// </summary>
    private async Task<(bool Renamed, string? Error)> MoveRetiredPrinciplesAsync(
        ISandbox sandbox, string contextName, CancellationToken ct)
    {
        var paths = BootstrapPaths.For(contextName);
        var reader = readerFactory.Create(sandbox);
        if (await reader.ExistsAsync(paths.Principles, ct)) return (false, null);
        if (!await reader.ExistsAsync(paths.RetiredPrinciples, ct)) return (false, null);

        var result = await sandbox.RunStepAsync(MoveStep(paths), progress: null, ct);
        if (result.ExitCode != 0)
            return (false,
                $"BootstrapMetaFiles: moving {paths.RetiredPrinciples} to {paths.Principles} "
                + "failed — " + (result.ErrorMessage ?? "unknown error"));

        logger.LogInformation(
            "{Context}: {Retired} renamed to {Current} before the round read it",
            contextName, paths.RetiredPrinciples, paths.Principles);
        return (true, null);
    }

    private static Step MoveStep(BootstrapPaths paths) =>
        new(Step.CurrentSchemaVersion, Guid.NewGuid(), StepKind.Run,
            Command: "sh", Args: ["-c", MoveScript(paths)], TimeoutSeconds: MoveTimeoutSeconds);

    /// <summary>
    /// <c>git mv</c> first, so the rename stays legible in the init pull request the operator
    /// reviews; an untracked file has no index entry to move, and a plain move keeps that from
    /// failing the round.
    /// </summary>
    internal static string MoveScript(BootstrapPaths paths) =>
        $"git mv -- '{paths.RetiredPrinciples}' '{paths.Principles}' 2>/dev/null || "
        + $"mv -- '{paths.RetiredPrinciples}' '{paths.Principles}'";
}
