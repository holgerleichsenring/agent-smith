using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Models.Preflight;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Contracts.Services;

namespace AgentSmith.Application.Services.Preflight.Run;

/// <summary>
/// 2026-10-25-c5a7a: a project that opted into root sandboxes says so on every run. The root
/// profile keeps the runtime's default capabilities, so the choice stays visible rather than
/// silent; the run is never failed for it.
/// </summary>
public sealed class SandboxRunsAsRootCheck : IRunPreflightCheck
{
    private const string Lever =
        "remove sandbox.run_as_root from the project once its prerequisites no longer need root";

    public string Name => "sandbox-runs-as-root";

    public Task<RunPreflightFinding> RunAsync(PipelineContext pipeline, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(pipeline);
        var project = pipeline.TryGet<ResolvedProject>(ContextKeys.ProjectConfig, out var p) ? p : null;
        var hasSandboxes = pipeline.TryGet<IReadOnlyDictionary<string, ISandbox>>(
            ContextKeys.Sandboxes, out var sandboxes) && sandboxes is { Count: > 0 };
        return Task.FromResult(project?.Sandbox?.RunAsRoot == true && hasSandboxes
            ? RunPreflightFinding.Warn(Name,
                $"project '{project.Name}' sets sandbox.run_as_root: its sandboxes run as root "
                + "with the default capabilities", Lever)
            : RunPreflightFinding.Pass(Name, "sandboxes run as uid 1000 with no capabilities"));
    }
}
