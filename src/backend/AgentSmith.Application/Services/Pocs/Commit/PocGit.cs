using AgentSmith.Application.Services.Handlers;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Entities;
using AgentSmith.Sandbox.Wire;

namespace AgentSmith.Application.Services.Pocs.Commit;

/// <summary>
/// 2026-11-23-933dc: one git command in a PoC folder write's sandbox, at the work root; a failure throws with git's own
/// message, which the run then names. A command given <c>remote</c> carries the repo's credential, because in a
/// blob-less clone a checkout or a rebase may fetch the blobs it reads from the host.
/// </summary>
public sealed class PocGit(IGitTokenResolver credentials)
{
    public async Task<string> RunAsync(
        ISandbox sandbox, string what, string[] args, CancellationToken ct, RepoConnection? remote = null)
    {
        ArgumentNullException.ThrowIfNull(sandbox);
        var credential = remote is null ? null : credentials.For(remote);
        var env = credential?.Env();
        string[] argv = credential is null ? args : [.. GitStepCredentials.Args(credential), .. args]; // 2026-11-25-d0e2
        return await RunStepAsync(sandbox, what, new Step(Step.CurrentSchemaVersion, Guid.NewGuid(), StepKind.Run,
            Command: "git", Args: argv, Env: env), ct);
    }

    public async Task<string> RunStepAsync(ISandbox sandbox, string what, Step step, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(sandbox);
        var result = await sandbox.RunStepAsync(step, null, ct);
        if (result.ExitCode != 0)
            throw new InvalidOperationException($"{what} failed (exit {result.ExitCode}): "
                + (result.ErrorMessage ?? result.OutputContent ?? string.Empty).Trim());
        return (result.OutputContent ?? string.Empty).Trim();
    }
}
