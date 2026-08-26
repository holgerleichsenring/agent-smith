using AgentSmith.Application.Services.Tools;

namespace AgentSmith.Application.Services;

/// <summary>
/// 2026-08-26-167c: decides whether a bootstrap round produced a context.yaml, and
/// says why when it did not. Returns the failure text, or null when the round may pass.
/// <para>
/// The question is "did THIS round write one", not "is one there". The round used to
/// ask the sandbox, and on a re-init the file is already on disk from last time — so
/// a round whose every write was refused reported green while the stale context
/// survived untouched. What is on disk is now only ever corroboration.
/// </para>
/// </summary>
public sealed class BootstrapContextWriteVerdict
{
    public string? Failure(
        string skillName, string contextYamlPath, ContextWriteOutcome outcome, bool onDisk)
    {
        ArgumentNullException.ThrowIfNull(outcome);
        if (outcome.Written) return onDisk ? null : NotOnDisk(skillName, contextYamlPath);
        return outcome.LastRefusal is null
            ? NeverCalled(skillName, contextYamlPath)
            : Refused(skillName, contextYamlPath, outcome, onDisk);
    }

    private static string NotOnDisk(string skillName, string path) =>
        $"BootstrapRound: skill '{skillName}' wrote {path} but it is not on the sandbox "
        + "afterwards. context.yaml is required.";

    private static string NeverCalled(string skillName, string path) =>
        $"BootstrapRound: skill '{skillName}' did not produce {path} — the "
        + $"{WriteContextYamlToolHost.ToolName} tool was not called. context.yaml is required.";

    private static string Refused(
        string skillName, string path, ContextWriteOutcome outcome, bool onDisk) =>
        $"BootstrapRound: skill '{skillName}' had its {WriteContextYamlToolHost.ToolName} "
        + $"write of {path} REFUSED, so this round produced no context.yaml"
        + (onDisk ? " and what is on the sandbox is the previous one, untouched" : string.Empty)
        + (outcome.BudgetExhausted ? ". The per-round refusal budget was exhausted" : string.Empty)
        + $". Last refusal: {outcome.LastRefusal}";
}
