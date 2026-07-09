using Microsoft.Extensions.AI;

namespace AgentSmith.Application.Services.Tools;

/// <summary>
/// Composes AITool lists from the p0145 hosts (FilesystemToolHost +
/// LogDecisionToolHost + HumanToolHost) plus p0154's in-process WebToolHost.
/// Replaces the pre-p0145 SandboxToolHost facade — same tool surfaces, no
/// facade layer. Pass <c>web: null</c> when the caller has no HttpClient
/// available (Scout / Bootstrap by default skip the web surface).
/// </summary>
public static class AgenticToolSurface
{
    /// <summary>Full agentic surface: fs + log + human + (optional) web + (optional) credentials + (optional) write_context_yaml.</summary>
    public static IList<AITool> ReadWriteWithHuman(
        FilesystemToolHost fs,
        LogDecisionToolHost log,
        HumanToolHost human,
        WebToolHost? web = null,
        GetArtifactCredentialsToolHost? credentials = null,
        WriteContextYamlToolHost? writeContextYaml = null) =>
        fs.GetTools(phase: null, investigatorMode: null)
            .Concat(log.GetTools(phase: null, investigatorMode: null))
            .Concat(human.GetTools(phase: null, investigatorMode: null))
            .Concat(web?.GetTools(phase: null, investigatorMode: null) ?? [])
            .Concat(credentials?.GetTools(phase: null, investigatorMode: null) ?? [])
            .Concat(writeContextYaml?.GetTools(phase: null, investigatorMode: null) ?? [])
            .Cast<AITool>()
            .ToList();

    /// <summary>Scout / investigator surface: read-only fs + (optional) web_fetch.</summary>
    public static IList<AITool> Scout(FilesystemToolHost fs, WebToolHost? web = null) =>
        fs.GetTools(Models.SkillExecutionPhase.Plan, investigatorMode: null)
            .Concat(web?.GetTools(phase: null, investigatorMode: null) ?? [])
            .Cast<AITool>()
            .ToList();

    /// <summary>
    /// p0278: scan/review surface — read-only fs (read_file / grep / find / list /
    /// directory_tree) + log_decision (for dropped-false-positive reasons). No
    /// write_file, edit, run_command, http_request, human, or web: a security scan must
    /// not mutate, build, or test the source. This is the structural reason a scan
    /// master can no longer run `dotnet test` or edit code.
    /// NOTE: the BootstrapDiscover phase is the ONLY filesystem tool set that excludes
    /// run_command + http_request (Plan/Investigate/Verify all keep run_command); we
    /// borrow its set here for the tool surface, not its bootstrap semantics.
    /// </summary>
    public static IList<AITool> Review(FilesystemToolHost fs, LogDecisionToolHost log) =>
        fs.GetTools(Models.SkillExecutionPhase.BootstrapDiscover, investigatorMode: null)
            .Concat(log.GetTools(phase: null, investigatorMode: null))
            .Cast<AITool>()
            .ToList();

    /// <summary>
    /// Bootstrap surface: fs read/write/list/grep + log_decision + (optional)
    /// write_context_yaml (no run, no human, no web). context.yaml MUST be written
    /// through write_context_yaml — write_file rejects context.yaml paths (p0193) —
    /// so the producer round is handed the typed tool here.
    /// </summary>
    public static IList<AITool> Bootstrap(
        FilesystemToolHost fs, LogDecisionToolHost log,
        WriteContextYamlToolHost? writeContextYaml = null) =>
        fs.GetTools(Models.SkillExecutionPhase.Bootstrap, investigatorMode: null)
            .Concat(log.GetTools(phase: null, investigatorMode: null))
            .Concat(writeContextYaml?.GetTools(phase: null, investigatorMode: null) ?? [])
            .Cast<AITool>()
            .ToList();

    /// <summary>
    /// p0161d: BootstrapDiscover surface: read-only filesystem (read_file,
    /// list_directory, directory_tree, grep_in_*, find_files) + ask_human
    /// (interactive transports only; HumanToolHost returns the ask_human
    /// tool, but it answers with a transport-not-configured error on
    /// headless runs so the LLM has to fail loud rather than guess).
    /// Explicitly no write_file, no run_command, no http_request.
    /// </summary>
    public static IList<AITool> BootstrapDiscover(FilesystemToolHost fs, HumanToolHost human) =>
        fs.GetTools(Models.SkillExecutionPhase.BootstrapDiscover, investigatorMode: null)
            .Concat(human.GetTools(phase: null, investigatorMode: null))
            .Cast<AITool>()
            .ToList();
}
