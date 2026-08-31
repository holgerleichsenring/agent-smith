using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Sandbox;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Handlers;

/// <summary>
/// p0513: does a declared verification stage have the files it was written against?
/// <para>
/// One declaration covers repositories of different shapes, and a command green on one
/// shape has no evidence at all on a repository carrying none of its files — it would
/// simply red. Verification stops at the first non-zero exit, so that red would hide
/// every real gate behind it. The stage therefore states the path it needs, and an
/// absent path SKIPS it rather than failing it.
/// </para>
/// <para>
/// A path, not a shape enum: enumerating repository shapes would put the taxonomy in
/// the binary and make every new declaration wait for a code change.
/// </para>
/// <para>
/// 2026-08-31-26d4: re-signatured onto <see cref="ContextYamlVerifyStage"/>, the
/// declaration that now supplies these commands.
/// </para>
/// </summary>
public sealed class DeclaredStagePresence(
    ISandboxFileReaderFactory readerFactory,
    ILogger<DeclaredStagePresence> logger)
{
    /// <summary>
    /// True when the stage may run: it declares no condition
    /// (<see cref="ContextYamlVerifyStage.WhenPresent"/> empty), or the path it declares
    /// exists under <paramref name="workdir"/> in the sandbox.
    /// </summary>
    public async Task<bool> IsSatisfiedAsync(
        string key, ContextYamlVerifyStage stage, ISandbox sandbox,
        string workdir, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(stage);
        if (string.IsNullOrWhiteSpace(stage.WhenPresent)) return true;

        var path = Combine(workdir, stage.WhenPresent);
        if (await readerFactory.Create(sandbox).ExistsAsync(path, cancellationToken)) return true;

        logger.LogInformation(
            "{Key}: the {Stage} command needs '{Path}', which this repository does not carry — "
            + "skipping it. It was never measured against this shape.",
            key, stage.Label, path);
        return false;
    }

    private static string Combine(string workdir, string relative)
    {
        var trimmed = relative.Trim().Replace('\\', '/').TrimStart('/');
        return $"{workdir.TrimEnd('/')}/{trimmed}";
    }
}
