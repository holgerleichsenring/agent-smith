using AgentSmith.Contracts.Models.Skills;
using AgentSmith.Contracts.Sandbox;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Handlers;

/// <summary>
/// p0513: does a profile command have the files it was measured against?
/// <para>
/// A domain word covers repositories of different shapes. p0505 measured each
/// command against the clean variant of ITS OWN shape, and a command green on one
/// shape has no evidence at all on a repository carrying none of its files — it
/// would simply red. Verification stops at the first non-zero exit, so that red
/// would hide every real gate behind it. The command therefore states the path it
/// needs, and an absent path SKIPS it rather than failing it.
/// </para>
/// <para>
/// A path, not a shape enum: enumerating repository shapes would put the taxonomy
/// in the binary and make every new profile wait for a code change.
/// </para>
/// </summary>
public sealed class ProfileCommandPresence(
    ISandboxFileReaderFactory readerFactory,
    ILogger<ProfileCommandPresence> logger)
{
    /// <summary>
    /// True when the command may run: it declares no condition, or the path it
    /// declares exists under <paramref name="workdir"/> in the sandbox.
    /// </summary>
    public async Task<bool> IsSatisfiedAsync(
        string key, string domain, DomainProfileCommand command, ISandbox sandbox,
        string workdir, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        if (string.IsNullOrWhiteSpace(command.WhenPresent)) return true;

        var path = Combine(workdir, command.WhenPresent);
        if (await readerFactory.Create(sandbox).ExistsAsync(path, cancellationToken)) return true;

        logger.LogInformation(
            "{Key}: domain '{Domain}' {Stage} command needs '{Path}', which this repository does "
            + "not carry — skipping it. It was never measured against this shape.",
            key, domain, command.Stage, path);
        return false;
    }

    private static string Combine(string workdir, string relative)
    {
        var trimmed = relative.Trim().Replace('\\', '/').TrimStart('/');
        return $"{workdir.TrimEnd('/')}/{trimmed}";
    }
}
