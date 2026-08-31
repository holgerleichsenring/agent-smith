using AgentSmith.Contracts.Sandbox;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Handlers;

/// <summary>
/// p0513: does a verification command have the files it was measured against?
/// <para>
/// One command list covers repositories of different shapes, and a command green on
/// one shape has no evidence at all on a repository carrying none of its files — it
/// would simply red. Verification stops at the first non-zero exit, so that red
/// would hide every real gate behind it. The command therefore states the path it
/// needs, and an absent path SKIPS it rather than failing it.
/// </para>
/// <para>
/// A path, not a shape enum: enumerating repository shapes would put the taxonomy
/// in the binary and make every new command list wait for a code change.
/// </para>
/// </summary>
public sealed class ProfileCommandPresence(
    ISandboxFileReaderFactory readerFactory,
    ILogger<ProfileCommandPresence> logger)
{
    /// <summary>
    /// True when the command may run: it declares no condition
    /// (<paramref name="whenPresent"/> empty), or the path it declares exists under
    /// <paramref name="workdir"/> in the sandbox.
    /// </summary>
    public async Task<bool> IsSatisfiedAsync(
        string key, string stage, string? whenPresent, ISandbox sandbox,
        string workdir, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(whenPresent)) return true;

        var path = Combine(workdir, whenPresent);
        if (await readerFactory.Create(sandbox).ExistsAsync(path, cancellationToken)) return true;

        logger.LogInformation(
            "{Key}: the {Stage} command needs '{Path}', which this repository does not carry — "
            + "skipping it. It was never measured against this shape.",
            key, stage, path);
        return false;
    }

    private static string Combine(string workdir, string relative)
    {
        var trimmed = relative.Trim().Replace('\\', '/').TrimStart('/');
        return $"{workdir.TrimEnd('/')}/{trimmed}";
    }
}
