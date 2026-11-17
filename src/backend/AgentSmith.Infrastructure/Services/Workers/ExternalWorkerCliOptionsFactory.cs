using AgentSmith.Contracts.Models.Configuration;

namespace AgentSmith.Infrastructure.Services.Workers;

/// <summary>
/// p0416: resolves how the external agent CLI is invoked for one agent. The agent's own
/// config carries it — <c>endpoint</c> is the binary, <c>model</c> is passed through to
/// the CLI, <c>network_timeout_seconds</c> bounds the call — so worker mode is configured
/// exactly where every other provider is configured. The env vars are the escape hatch
/// for a machine-local binary or extra CLI flags.
/// </summary>
public sealed class ExternalWorkerCliOptionsFactory
{
    public const string BinaryEnv = "AGENTSMITH_WORKER_CLI";
    public const string ExtraArgsEnv = "AGENTSMITH_WORKER_CLI_ARGS";
    public const string WorkingDirectoryEnv = "AGENTSMITH_WORKER_CWD";
    public const string DefaultBinary = "claude";

    /// <summary>2026-11-17-166ac: the worker's own empty directory under the temp directory.</summary>
    public const string WorkingDirectoryName = "agentsmith-worker";

    /// <summary>2026-11-17-166ac: Claude Code's flags that leave the worker the model and nothing else — no
    /// built-in tools, no MCP servers, no session written per call.</summary>
    public static readonly IReadOnlyList<string> IsolationArguments =
        ["--tools", "", "--strict-mcp-config", "--no-session-persistence"];

    public ExternalWorkerCliOptions Create(AgentConfig agent, ModelAssignment assignment)
    {
        var binary = ResolveBinary(agent);
        return new(binary,
            BuildArguments(agent, binary, assignment),
            TimeSpan.FromSeconds(Math.Max(1, agent.NetworkTimeoutSeconds)),
            ResolveWorkingDirectory());
    }

    private static string ResolveBinary(AgentConfig agent) =>
        FirstNonEmpty(Environment.GetEnvironmentVariable(BinaryEnv), agent.Endpoint) ?? DefaultBinary;

    private static IReadOnlyList<string> BuildArguments(
        AgentConfig agent, string binary, ModelAssignment assignment)
    {
        List<string> arguments = ["-p"];
        if (!string.IsNullOrWhiteSpace(assignment.Model)) arguments.AddRange(["--model", assignment.Model]);
        if (WantsStructuredResult(agent, binary)) arguments.AddRange(["--output-format", "json"]);
        if (IsClaudeCode(agent, binary)) arguments.AddRange(IsolationArguments);
        var extra = Environment.GetEnvironmentVariable(ExtraArgsEnv);
        if (!string.IsNullOrWhiteSpace(extra))
            arguments.AddRange(extra.Split(' ', StringSplitOptions.RemoveEmptyEntries
                | StringSplitOptions.TrimEntries));
        return arguments;
    }

    /// <summary>
    /// 2026-09-01-b0d7: the structured result is OPT-IN. Worker mode promises that any
    /// binary reading a prompt on stdin and printing an answer works, and an unknown binary
    /// handed an unknown flag exits non-zero — which the guard turns into a dead run.
    /// Unset means on for the default CLI, which documents the flag, off for anything else.
    /// </summary>
    private static bool WantsStructuredResult(AgentConfig agent, string binary) =>
        agent.WorkerStructuredResult ?? IsDefaultBinary(binary);

    /// <summary>
    /// 2026-11-17-166ac: the binary takes Claude Code's flags — the default binary, or one the agent opted into
    /// the structured result for (a wrapper script around the CLI). Turning the structured result off on the
    /// default binary keeps the isolation: the name still says it is Claude Code.
    /// </summary>
    public static bool IsClaudeCode(AgentConfig agent, string binary) =>
        agent.WorkerStructuredResult == true || IsDefaultBinary(binary);

    private static bool IsDefaultBinary(string binary) =>
        string.Equals(Path.GetFileNameWithoutExtension(binary), DefaultBinary, StringComparison.OrdinalIgnoreCase);

    // A neutral directory by default: the worker answers a model call, so it must not
    // pick up the project instructions or the source tree of the repo under change.
    // 2026-11-17-166ac: its own empty directory, not the temp directory itself — that holds every
    // sandbox clone, and a worker with file tools scanned it in the replay. One stable directory, not
    // one per call: the CLI names its working directory in its own prompt and keeps state per directory.
    private static string ResolveWorkingDirectory() =>
        FirstNonEmpty(Environment.GetEnvironmentVariable(WorkingDirectoryEnv))
        ?? Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), WorkingDirectoryName)).FullName;

    private static string? FirstNonEmpty(params string?[] candidates) =>
        candidates.FirstOrDefault(c => !string.IsNullOrWhiteSpace(c));
}
