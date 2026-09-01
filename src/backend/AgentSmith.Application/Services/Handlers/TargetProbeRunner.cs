using System.Text;
using AgentSmith.Application.Services.Sandbox;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Sandbox.Wire;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Handlers;

/// <summary>
/// 2026-09-01-379a: asks ONE declared probe whether its target answers, and returns an exit
/// code and nothing else.
/// <para>
/// The output never leaves this class. A failing step's output travels into the failure
/// reason, the per-repository result document and a comment on the external ticket, and the
/// masker only knows values the framework holds — by design it never holds an injected
/// credential, so a value it does not know is one it cannot replace. Returning the tail and
/// asking the caller to be careful would put that rule in a comment; returning an int puts
/// it in the type. The tail goes to the operator-facing log here, once.
/// </para>
/// </summary>
public sealed class TargetProbeRunner(ILogger<TargetProbeRunner> logger)
{
    // A probe asks one question of one endpoint. It is not a build, and a target that takes
    // minutes to say hello has already answered the question the run was asking.
    private const int ProbeTimeoutSeconds = 120;
    private const int OutputTailChars = 4000;

    /// <summary>The probe's exit code. Zero = the target answered.</summary>
    public async Task<int> AskAsync(
        string key, ISandbox sandbox, ContextTargetProbe probe, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(sandbox);
        ArgumentNullException.ThrowIfNull(probe);
        logger.LogInformation(
            "{Key}: asking {Target} whether it answers via '{Command}' at {Cwd}",
            key, probe.Target, probe.Command, probe.Workdir);

        // The verify runner's shell, not the prerequisite tokenizer: a declared command is a
        // command LINE, and an injected credential is reached as $VAR, which only a shell
        // expands. Tokenising it would hand `&&` and `$SF_USERNAME` to the binary verbatim.
        var step = new Step(
            Step.CurrentSchemaVersion, Guid.NewGuid(), StepKind.Run,
            Command: "/bin/sh", Args: ["-c", probe.Command],
            WorkingDirectory: probe.Workdir, TimeoutSeconds: ProbeTimeoutSeconds);

        var streamed = new StringBuilder();
        var progress = new SyncProgress<StepEvent>(ev =>
        {
            if (ev.Kind is StepEventKind.Stdout or StepEventKind.Stderr)
                streamed.AppendLine(ev.Line);
        });

        var result = await sandbox.RunStepAsync(step, progress, ct);
        if (result.ExitCode != 0) LogRefusal(key, probe, result, streamed);
        return result.ExitCode;
    }

    private void LogRefusal(
        string key, ContextTargetProbe probe, StepResult result, StringBuilder streamed)
    {
        var output = Combine(result.OutputContent, result.ErrorMessage);
        if (string.IsNullOrWhiteSpace(output)) output = streamed.ToString().TrimEnd();
        logger.LogError(
            "{Key}: {Target} refused '{Command}' (exit {Exit}) at {Cwd}:\n{Output}",
            key, probe.Target, probe.Command, result.ExitCode, probe.Workdir,
            Tail(output, OutputTailChars));
    }

    private static string Combine(string? outputContent, string? errorMessage) =>
        string.Join("\n", new[] { outputContent, errorMessage }
            .Where(part => !string.IsNullOrWhiteSpace(part)));

    private static string Tail(string text, int maxChars) =>
        text.Length <= maxChars ? text : text[^maxChars..];
}
