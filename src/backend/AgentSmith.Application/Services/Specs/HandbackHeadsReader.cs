using AgentSmith.Application.Services.Handlers;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Domain.Entities;
using AgentSmith.Sandbox.Wire;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Specs;

/// <summary>
/// 2026-10-12-3a36b: THE REMOTE HEAD AT PARKING IS THE REFERENCE. When a contradiction parks, the
/// ticket branch's <c>origin/&lt;branch&gt;</c> is read in each scoped repository's sandbox —
/// after the hand-back's own revision was pushed, so our last commit is inside it — and stored
/// with the hand-back. A repository whose branch is not on the remote gets no entry; a later probe
/// skips it and says so.
/// </summary>
public sealed class HandbackHeadsReader(SandboxTargets targets, ILogger<HandbackHeadsReader> logger)
{
    private const int TimeoutSeconds = 60;

    /// <summary>Repository name to the sha its remote branch names, for every scoped repository
    /// whose sandbox can answer.</summary>
    public async Task<IReadOnlyDictionary<string, string>> ReadAsync(
        PipelineContext pipeline, IReadOnlyList<RepoConnection> repos, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(pipeline);
        ArgumentNullException.ThrowIfNull(repos);
        var heads = new Dictionary<string, string>(StringComparer.Ordinal);
        var branch = pipeline.TryGet<Repository>(ContextKeys.Repository, out var r) ? r?.CurrentBranch.Value : null;
        if (string.IsNullOrWhiteSpace(branch)) return heads;
        foreach (var repo in repos.Where(repo => !string.IsNullOrWhiteSpace(repo.Name)))
        {
            var sandbox = targets.SandboxesForRepo(pipeline, repo).Select(m => m.Value).FirstOrDefault();
            if (sandbox is null) continue;
            if (await RemoteHeadAsync(sandbox, branch, cancellationToken) is { } sha) heads[repo.Name!] = sha;
            else logger.LogInformation(
                "{Branch} is not on {Repo}'s remote — no head is stored for it with the hand-back", branch, repo.Name);
        }
        return heads;
    }

    private static async Task<string?> RemoteHeadAsync(ISandbox sandbox, string branch, CancellationToken ct)
    {
        var result = await sandbox.RunStepAsync(
            new Step(Step.CurrentSchemaVersion, Guid.NewGuid(), StepKind.Run, Command: "git",
                Args: ["rev-parse", "--verify", "--quiet", $"refs/remotes/origin/{branch}"],
                WorkingDirectory: Repository.SandboxWorkPath, TimeoutSeconds: TimeoutSeconds),
            progress: null, ct);
        var sha = (result.OutputContent ?? string.Empty).Trim();
        return result.ExitCode == 0 && sha.Length > 0 ? sha : null;
    }
}
