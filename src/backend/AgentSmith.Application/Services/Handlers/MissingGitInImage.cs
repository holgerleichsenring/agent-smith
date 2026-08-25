using AgentSmith.Sandbox.Wire;

namespace AgentSmith.Application.Services.Handlers;

/// <summary>
/// 2026-08-25-014d: the one checkout failure that is not about the repository —
/// the toolchain image the sandbox runs has no git on PATH. It is named here so
/// the operator reads the cause instead of an exit code, and so no part of the
/// product has to guess at it from an image's tag beforehand.
/// </summary>
internal static class MissingGitInImage
{
    // The sandbox agent execs the command directly (ProcessRunner), so a missing
    // binary never reaches an exit code: Process.Start throws and the agent
    // reports "failed to start 'git': …". A backend that goes through a shell
    // reports the POSIX 127 instead. Both mean the same thing.
    private const string StartFailure = "failed to start 'git'";
    private const int ShellCommandNotFound = 127;

    public const string Cause =
        "the sandbox's toolchain image has no git on PATH. A repository is cloned INSIDE "
        + "its own sandbox, so the image named by this context's stack.image (or brought by "
        + "its meta.domain profile) has to carry git — agent-smith cannot install one into a "
        + "running sandbox. Name an image variant that ships git.";

    /// <summary>Is this step result the image lacking git rather than the clone failing?</summary>
    public static bool Explains(StepResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        if (result.ErrorMessage is not { } message) return false;
        return message.Contains(StartFailure, StringComparison.Ordinal)
            || (result.ExitCode == ShellCommandNotFound
                && message.Contains("git", StringComparison.OrdinalIgnoreCase));
    }
}
