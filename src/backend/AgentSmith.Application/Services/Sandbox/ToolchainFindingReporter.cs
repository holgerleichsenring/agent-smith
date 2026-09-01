using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Sandbox;

/// <summary>
/// 2026-08-31-7097: says what a sandbox's declared stages asked for and did not find.
/// A missing binary is a WARNING with the image, the binary and the stage that named
/// it — never a failure: the derivation cannot read every command shape, and the
/// harness tiers deliberately run images that carry almost nothing.
/// </summary>
public sealed class ToolchainFindingReporter(ILogger<ToolchainFindingReporter> logger)
{
    private const string NoImage =
        "no toolchain image (this run's sandbox backend executes on the host)";

    /// <summary>Warns for every derived binary the sweep did not report, names the
    /// stages whose command shape was not read, and returns those findings as the
    /// lines the master's toolchain section carries.</summary>
    public IReadOnlyList<string> Report(
        string sandboxName, string? image, DeclaredStageDerivation derivation, string? probeStdout)
    {
        ArgumentNullException.ThrowIfNull(derivation);
        if (derivation.IsEmpty || string.IsNullOrWhiteSpace(probeStdout)) return [];
        var lines = new List<string>();
        var where = image ?? NoImage;
        foreach (var missing in MissingBinaries.In(probeStdout, derivation))
        {
            logger.LogWarning(
                "{Sandbox}: `{Binary}`, named by the declared '{Stage}' verify stage of "
                + "context '{Context}', is not on PATH in {Image}. The stage will fail on "
                + "it; the run continues, because this probe reports and does not gate.",
                sandboxName, missing.Binary, missing.StageLabel, missing.ContextName, where);
            lines.Add(
                $"`{missing.Binary}` (declared by the '{missing.StageLabel}' stage of "
                + $"context '{missing.ContextName}') was NOT found in {where}");
        }
        lines.AddRange(Unread(sandboxName, derivation));
        return lines;
    }

    // A command shape the derivation cannot read is said out loud. A silent partial
    // list is what makes a probe look like a guarantee it is not.
    private IEnumerable<string> Unread(string sandboxName, DeclaredStageDerivation derivation)
    {
        if (derivation.Unprobed.Count == 0) yield break;
        var stages = string.Join(", ",
            derivation.Unprobed.Select(u => $"'{u.StageLabel}' ({u.ContextName}): {u.Command}"));
        logger.LogInformation(
            "{Sandbox}: {Count} declared verify stage(s) name no binary this probe can look "
            + "for and were NOT checked: {Stages}", sandboxName, derivation.Unprobed.Count, stages);
        yield return
            $"Not checked (the command names no bare binary): {stages}";
    }
}
