using Microsoft.Extensions.AI;

namespace AgentSmith.Application.Services.Tools;

/// <summary>
/// Composes AITool lists from the p0145 hosts (FilesystemToolHost +
/// LogDecisionToolHost + HumanToolHost) plus p0154's in-process WebToolHost.
/// Replaces the pre-p0145 SandboxToolHost facade — same tool surfaces, no
/// facade layer. Pass <c>web: null</c> when the caller has no HttpClient
/// available (Scout / Bootstrap by default skip the web surface).
/// </summary>
public sealed class AgenticToolSurface
{
    /// <summary>Full agentic surface: fs + log + human + (optional) web + (optional) credentials + (optional) write_context_yaml + (optional) memory recall/remember (p0380).</summary>
    public IList<AITool> ReadWriteWithHuman(
        FilesystemToolHost fs,
        LogDecisionToolHost log,
        IToolHost human,
        WebToolHost? web = null,
        GetArtifactCredentialsToolHost? credentials = null,
        WriteContextYamlToolHost? writeContextYaml = null,
        MemoryRecallToolHost? recall = null,
        MemoryWriteToolHost? remember = null) =>
        fs.GetTools(phase: null, investigatorMode: null)
            .Concat(log.GetTools(phase: null, investigatorMode: null))
            .Concat(human.GetTools(phase: null, investigatorMode: null))
            .Concat(web?.GetTools(phase: null, investigatorMode: null) ?? [])
            .Concat(credentials?.GetTools(phase: null, investigatorMode: null) ?? [])
            .Concat(writeContextYaml?.GetTools(phase: null, investigatorMode: null) ?? [])
            .Concat(recall?.GetTools(phase: null, investigatorMode: null) ?? [])
            .Concat(remember?.GetTools(phase: null, investigatorMode: null) ?? [])
            .Cast<AITool>()
            .ToList();

    /// <summary>Scout / investigator surface: read-only fs + (optional) web_fetch.
    /// 2026-08-27-3eb1: every result bounded — the sweep is one message list.</summary>
    public IList<AITool> Scout(FilesystemToolHost fs, WebToolHost? web = null) =>
        fs.GetTools(Models.SkillExecutionPhase.Plan, investigatorMode: null)
            .Concat(web?.GetTools(phase: null, investigatorMode: null) ?? [])
            .Select(BoundedResultTool.Wrap)
            .ToList();

    /// <summary>
    /// p0278: scan/review surface — read-only fs (read_file / grep / find / list /
    /// directory_tree) + log_decision (for dropped-false-positive reasons) + web_fetch +
    /// http_request. No write_file, edit, or run_command: a scan must not mutate, build,
    /// or test the SOURCE — that structural rule stands.
    /// p0353: web_fetch (read-only GET) and http_request (any method) ARE on the surface:
    /// an api-security / security master's job is to reach the target API and vendor
    /// advisories. Reaching an external endpoint is not mutating the source; run_command
    /// (which builds/executes the repo) is the thing that stays excluded.
    /// </summary>
    /// <para>p0380: recall joins this surface (a memory read mutates nothing) and
    /// remember joins as a PROPOSAL tool — it writes ONLY run-record-class
    /// .agentsmith/memory/ paths, so "a scan modifies no code" stands; ratified
    /// FP-dismissals recalled here are the compounding scan payoff.</para>
    public IList<AITool> Review(
        FilesystemToolHost fs, LogDecisionToolHost log, WebToolHost? web = null,
        MemoryRecallToolHost? recall = null, MemoryWriteToolHost? remember = null) =>
        fs.GetTools(Models.SkillExecutionPhase.BootstrapDiscover, investigatorMode: null)
            .Append(fs.HttpRequestTool())
            .Concat(log.GetTools(phase: null, investigatorMode: null))
            .Concat(web?.GetTools(phase: null, investigatorMode: null) ?? [])
            .Concat(recall?.GetTools(phase: null, investigatorMode: null) ?? [])
            .Concat(remember?.GetTools(phase: null, investigatorMode: null) ?? [])
            .Cast<AITool>()
            .ToList();

    /// <summary>
    /// p0315b: spec-dialog design-partner surface — content reads only
    /// (read_file, grep_in_*, list_directory, directory_tree) + ask_human +
    /// (optional) web_fetch (research public docs while drafting). find_files is
    /// dropped from the BootstrapDiscover set because it shells out via a Run step,
    /// which the read-only source sandbox refuses; grep + directory_tree cover
    /// discovery. No write, no run, no log_decision (a conversation records no run
    /// decisions).
    /// </summary>
    public IList<AITool> SpecDialog(
        FilesystemToolHost fs, IToolHost human, WebToolHost? web = null,
        MemoryRecallToolHost? recall = null, MemoryWriteToolHost? remember = null) =>
        fs.GetTools(Models.SkillExecutionPhase.BootstrapDiscover, investigatorMode: null)
            .Where(t => !string.Equals(t.Name, "find_files", StringComparison.Ordinal))
            .Append(fs.HttpRequestTool()) // p0353: reach external endpoints while drafting; still no run/write
            .Concat(human.GetTools(phase: null, investigatorMode: null))
            .Concat(web?.GetTools(phase: null, investigatorMode: null) ?? [])
            // p0380: recall for grounding + remember as a memory-only proposal.
            .Concat(recall?.GetTools(phase: null, investigatorMode: null) ?? [])
            .Concat(remember?.GetTools(phase: null, investigatorMode: null) ?? [])
            .Cast<AITool>()
            .ToList();

    /// <summary>
    /// Bootstrap surface: fs read/write/list/grep + log_decision + (optional)
    /// write_context_yaml (no run, no human, no web). context.yaml MUST be written
    /// through write_context_yaml — write_file rejects context.yaml paths (p0193) —
    /// so the producer round is handed the typed tool here.
    /// </summary>
    public IList<AITool> Bootstrap(
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
    public IList<AITool> BootstrapDiscover(FilesystemToolHost fs, HumanToolHost human) =>
        fs.GetTools(Models.SkillExecutionPhase.BootstrapDiscover, investigatorMode: null)
            .Concat(human.GetTools(phase: null, investigatorMode: null))
            .Cast<AITool>()
            .ToList();
}
