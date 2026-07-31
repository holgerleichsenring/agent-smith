using AgentSmith.Application.Services.Handlers;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Contracts.WorkSpecs;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.WorkSpecs;

/// <summary>
/// p0390: reads the checked-out spec.yaml + spec.md and the sha of the last
/// commit on the spec path. A missing or unparseable file is "no previous
/// revision", never a failure — the run then derives a first revision.
/// </summary>
public sealed class WorkSpecReader(
    ISandboxFileReaderFactory readerFactory,
    SandboxGitOperations gitOps,
    IWorkSpecSerializer serializer,
    ILogger<WorkSpecReader> logger) : IWorkSpecReader
{
    public async Task<WorkSpecReadResult?> ReadAsync(
        PipelineContext pipeline, RepoConnection carryingRepo, WorkSpecKey key,
        CancellationToken cancellationToken)
    {
        var matches = SandboxTargets.SandboxesForRepo(pipeline, carryingRepo);
        if (matches.Count == 0)
        {
            logger.LogDebug("No sandbox for {Repo} — no previous work spec to read", carryingRepo.Name);
            return null;
        }
        var sandbox = matches[0].Value;
        var files = readerFactory.Create(sandbox);
        var yaml = await files.TryReadAsync(key.SpecPath, cancellationToken);
        var spec = serializer.Parse(yaml ?? string.Empty);
        if (spec is null)
        {
            if (!string.IsNullOrWhiteSpace(yaml))
                logger.LogWarning(
                    "{Path} exists on the branch but did not parse — deriving a first revision instead",
                    key.SpecPath);
            return null;
        }
        var samples = await files.TryReadAsync(key.SamplesPath, cancellationToken) ?? string.Empty;
        var sha = await gitOps.GetLastCommitForPathAsync(sandbox, key.Directory, cancellationToken);
        return new WorkSpecReadResult(new WorkSpecArtifact(spec, samples), sha);
    }
}
