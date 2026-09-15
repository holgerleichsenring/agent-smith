using AgentSmith.Application.Models;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Domain.Models;
using AgentSmith.Sandbox.Wire;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services;

/// <summary>
/// 2026-09-15-d66f: writes the enforcement artefacts a language delta declares into the target
/// repository, deciding preserve-existing PER PATH.
/// <para>
/// Per path is the whole point. The principles transfer answers preserve-or-write once for the
/// round, and applied to a file SET that answer would suppress every artefact in any repository
/// that already carries a ratified principles.md — which is exactly the established repositories
/// the artefacts exist for.
/// </para>
/// <para>
/// Carved out rather than grown into <see cref="BootstrapPrinciplesTransfer"/>, which has too
/// little room under its length limit for a loop, a path rule and an existence probe — the same
/// reason <see cref="Handlers.BootstrapMetaFiles"/> was carved out of the round handler.
/// </para>
/// </summary>
public sealed class BootstrapArtefactWriter(
    ISandboxFileReaderFactory readerFactory,
    ILogger<BootstrapArtefactWriter> logger)
{
    private const int WriteTimeoutSeconds = 30;

    /// <summary>
    /// One outcome per declared artefact, in declaration order.
    /// <para>
    /// <paramref name="writtenEarlierThisRun"/> is the set of paths an earlier round of THIS run
    /// already wrote in this repository. A repository fans out one round per component against a
    /// single checkout, so a repository-root artefact written by the first component is present
    /// for the second — and reporting that as "preserved" would tell an operator their file was
    /// ratified when this very run created it. The caller owns the set, because it is a fact
    /// about the run and not about any one round.
    /// </para>
    /// </summary>
    public async Task<IReadOnlyList<ArtefactWrite>> ApplyAsync(
        ISandbox sandbox, string repoName, string contextName,
        IReadOnlyList<PrinciplesArtefact> artefacts,
        IReadOnlySet<string> writtenEarlierThisRun,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(artefacts);
        ArgumentNullException.ThrowIfNull(writtenEarlierThisRun);
        if (artefacts.Count == 0) return [];

        var reader = readerFactory.Create(sandbox);
        var writes = new List<ArtefactWrite>(artefacts.Count);
        foreach (var artefact in artefacts)
            writes.Add(await ApplyOneAsync(
                sandbox, reader, repoName, contextName, artefact, writtenEarlierThisRun, cancellationToken));
        return writes;
    }

    private async Task<ArtefactWrite> ApplyOneAsync(
        ISandbox sandbox, ISandboxFileReader reader, string repoName, string contextName,
        PrinciplesArtefact artefact, IReadOnlySet<string> writtenEarlierThisRun,
        CancellationToken cancellationToken)
    {
        if (Refusal(artefact.Path) is { } refusal)
        {
            logger.LogWarning(
                "{Repo}/{Context}: artefact path '{Path}' refused — {Reason}",
                repoName, contextName, artefact.Path, refusal);
            return new ArtefactWrite(artefact.Path, ArtefactStatus.Refused, refusal);
        }

        if (await reader.ExistsAsync(artefact.Path, cancellationToken))
            return new ArtefactWrite(
                artefact.Path,
                writtenEarlierThisRun.Contains(artefact.Path)
                    ? ArtefactStatus.AlreadyWrittenThisRound
                    : ArtefactStatus.PreservedExisting);

        var step = new Step(
            Step.CurrentSchemaVersion, Guid.NewGuid(), StepKind.WriteFile,
            TimeoutSeconds: WriteTimeoutSeconds,
            Path: artefact.Path, Content: artefact.Content);
        var result = await sandbox.RunStepAsync(step, progress: null, cancellationToken);
        if (result.ExitCode != 0)
        {
            var reason = result.ErrorMessage ?? "unknown error";
            logger.LogWarning(
                "{Repo}/{Context}: writing artefact {Path} failed — {Reason}",
                repoName, contextName, artefact.Path, reason);
            return new ArtefactWrite(artefact.Path, ArtefactStatus.Refused, reason);
        }

        logger.LogInformation(
            "{Repo}/{Context}: artefact {Path} written from the composed delta",
            repoName, contextName, artefact.Path);
        return new ArtefactWrite(artefact.Path, ArtefactStatus.Written);
    }

    /// <summary>
    /// Why this path may not be written, or null. The rule is the domain's own
    /// <see cref="FilePath"/> — rooted paths and parent traversal — rather than a second copy of
    /// it here, which would be a second place for the two to disagree.
    /// </summary>
    private static string? Refusal(string path)
    {
        try
        {
            _ = new FilePath(path);
            return null;
        }
        catch (ArgumentException ex)
        {
            return ex.Message;
        }
    }
}
