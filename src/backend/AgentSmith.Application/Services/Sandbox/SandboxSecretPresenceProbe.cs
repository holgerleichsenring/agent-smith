using AgentSmith.Contracts.Sandbox;
using AgentSmith.Sandbox.Wire;

namespace AgentSmith.Application.Services.Sandbox;

/// <inheritdoc />
public sealed class SandboxSecretPresenceProbe : ISandboxSecretPresenceProbe
{
    private const int TimeoutSeconds = 60;

    public async Task<IReadOnlyList<string>> MissingAsync(
        ISandbox sandbox, ResolvedSandboxSecrets secrets, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(sandbox);
        ArgumentNullException.ThrowIfNull(secrets);
        var script = Script(secrets);
        if (script.Length == 0) return [];
        var step = new Step(
            Step.CurrentSchemaVersion, Guid.NewGuid(), StepKind.Run,
            Command: "sh", Args: ["-c", script], TimeoutSeconds: TimeoutSeconds);
        var result = await sandbox.RunStepAsync(step, null, cancellationToken);
        return Names(result.OutputContent);
    }

    /// <summary>
    /// One test per declaration, each echoing only the NAME it was given. <c>-n</c> on the
    /// variable and <c>-s</c> on the file both answer "arrived and is not blank" without the
    /// content ever being expanded into an argument, a log line or this method's result. The
    /// declaration check refuses a mount carrying a quote, so the paths quote cleanly here.
    /// </summary>
    internal static string Script(ResolvedSandboxSecrets secrets) =>
        string.Join("\n", secrets.Env
            .Select(e => $"[ -n \"${{{e.EnvName}:-}}\" ] || echo {e.EnvName}")
            .Concat(secrets.Files.Select(f => $"[ -s '{f.MountPath}' ] || echo '{f.MountPath}'")));

    internal static IReadOnlyList<string> Names(string? output) =>
        (output ?? string.Empty)
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToList();
}
