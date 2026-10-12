using AgentSmith.Contracts.Sandbox;
using AgentSmith.Domain.Entities;
using AgentSmith.Sandbox.Wire;

namespace AgentSmith.Application.Services.Structure;

/// <summary>
/// 2026-10-06-8565c: the tracked files under a workdir, repository-relative, as git lists them —
/// build output and generated directories are not in git, so no exclusion list is kept here.
/// A listing at the sandbox's capture cap may be incomplete and is refused: a partial baseline
/// would tolerate nothing it missed.
/// </summary>
public sealed class StructureSourceListing
{
    // The sandbox agent stops capturing stdout at this many characters (StepExecutor). A -z
    // listing is one line, so it is never cut — it overruns instead, and reaching the cap is
    // the only sign.
    private const int CaptureCap = 1_000_000;

    public async Task<(IReadOnlyList<string> Paths, string? Refusal)> ListAsync(
        ISandbox sandbox, string workdir, CancellationToken cancellationToken)
    {
        var step = new Step(Step.CurrentSchemaVersion, Guid.NewGuid(), StepKind.Run,
            Command: "git", Args: ["ls-files", "-z", "--full-name", "--", workdir.Length == 0 ? "." : workdir],
            WorkingDirectory: Repository.SandboxWorkPath);
        var result = await sandbox.RunStepAsync(step, null, cancellationToken);
        if (result.ExitCode != 0) return ([], "the source is not a git checkout");
        var output = (result.OutputContent ?? string.Empty).TrimEnd('\n');
        if (output.Length >= CaptureCap) return ([], "the file listing reached the capture cap and may be incomplete");
        return ([.. output.Split('\0', StringSplitOptions.RemoveEmptyEntries)], null);
    }
}
