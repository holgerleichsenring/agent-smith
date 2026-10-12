using AgentSmith.Application.Services.Handlers;
using AgentSmith.Application.Services.Sandbox;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Models.Preflight;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Contracts.Services;

namespace AgentSmith.Application.Services.Preflight.Run;

/// <summary>
/// 2026-10-12-6bfaa: says on the run when a sandbox landed on the generic fallback image — git and
/// no language toolchain. Reads the image the backend recorded, so the in-process backend — the
/// host's toolchain — is never accused, and an image a link of the chain NAMED is a choice.
/// <para>
/// 2026-10-12-50bda: a context that fell through to it while DECLARING verify stages fails the run:
/// nothing it declares can run there, and the run would learn that as a command-not-found at
/// verify, half an hour later. A context declaring nothing to build keeps the warning — a
/// repository without a language legitimately runs on that image. A context whose own image was
/// refused is the context-image-trust check's to report.
/// </para>
/// </summary>
public sealed class ToolchainImageFallbackCheck(SandboxImageChain chain) : IRunPreflightCheck
{
    private const string Lever =
        "merge the branch that carries .agentsmith/contexts/ (an open init pull request) into the default "
        + "branch, or declare stack.lang / stack.image in the context.yaml, or set sandbox.images.<lang> or "
        + "sandbox.toolchain_image for the project";

    private const string BlockingLever =
        "name an image that carries the toolchain the verify stages call: stack.image in the context.yaml, "
        + "or sandbox.images.<lang> or sandbox.toolchain_image in the project's Sandbox tab; to keep the "
        + "generic image on purpose, name it in sandbox.images.<lang> or sandbox.toolchain_image";

    public string Name => "toolchain-image";

    public Task<RunPreflightFinding> RunAsync(PipelineContext pipeline, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(pipeline);
        var fellBack = FellBack(pipeline);
        var blocking = fellBack.Where(s => s.Declares).Select(s => s.Line).ToList();
        if (blocking.Count > 0)
            return Task.FromResult(RunPreflightFinding.Fail(Name,
                $"the generic image '{SandboxImageChain.GenericFallbackImage}' carries git and no language "
                + "toolchain, and these contexts declare verify stages it cannot run — "
                + string.Join("; ", blocking), BlockingLever));
        return Task.FromResult(fellBack.Count == 0
            ? RunPreflightFinding.Pass(Name, "every sandbox runs a toolchain image chosen for it")
            : RunPreflightFinding.Warn(Name,
                $"the generic image '{SandboxImageChain.GenericFallbackImage}' carries git and no language "
                + "toolchain, so nothing can be built or tested in it — "
                + string.Join("; ", fellBack.Select(s => s.Line)), Lever));
    }

    private List<(string Line, bool Declares)> FellBack(PipelineContext pipeline)
    {
        var project = pipeline.TryGet<ResolvedProject>(ContextKeys.ProjectConfig, out var p) && p is not null
            ? p : new ResolvedProject();
        var discoveries = pipeline.TryGet<IReadOnlyDictionary<string, RemoteContextDiscovery>>(
            ContextKeys.SandboxDiscoveries, out var d) ? d : null;
        var found = new List<(string Line, bool Declares)>();
        foreach (var key in Images(pipeline)
                     .Where(kv => kv.Value == SandboxImageChain.GenericFallbackImage).Select(kv => kv.Key))
        {
            var contexts = SandboxContextList.InOr(pipeline, key, discoveries?.GetValueOrDefault(key));
            if (contexts.Count == 0) found.Add(($"sandbox '{key}': no context was resolved for it", false));
            found.AddRange(contexts
                .Where(c => chain.Decide(project, c.Language, c.ToolchainImage).FellThrough)
                .Select(c => ($"sandbox '{key}': {Why(c)}", c.Verify is { Count: > 0 })));
        }
        return found;
    }

    private static IReadOnlyDictionary<string, string> Images(PipelineContext pipeline) =>
        pipeline.TryGet<IReadOnlyDictionary<string, string>>(ContextKeys.SandboxImages, out var images)
        && images is not null ? images : new Dictionary<string, string>();

    private static string Why(RemoteContextDiscovery discovery) => discovery switch
    {
        { Language: null } => $"context '{discovery.ContextName}' names no language — {discovery.Provenance ?? "no stack.lang declared"}",
        _ => $"context '{discovery.ContextName}' declares lang={discovery.Language}, for which no image is configured, "
            + $"declared or catalogued ({discovery.Provenance ?? "provenance not recorded"})",
    } + (discovery.Verify is { Count: > 0 } stages
        ? $", and declares {stages.Count} verify stage(s) ({string.Join(", ", stages.Select(s => s.Command))})"
        : string.Empty);
}
