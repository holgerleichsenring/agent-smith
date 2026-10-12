using AgentSmith.Application.Services.Sandbox;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Models.Preflight;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Contracts.Services;

namespace AgentSmith.Application.Services.Preflight.Run;

/// <summary>
/// 2026-10-12-6bfaa: says on the run when a sandbox landed on the generic fallback image — git and
/// no language toolchain. The image chain only logged it on the server, so a run that could build
/// nothing learned it as a command-not-found half an hour in, and read it as its own failure.
/// <para>
/// A REPORT, never a refusal: a repository with no language legitimately runs on that image. It
/// reads the image the backend recorded, so the in-process backend — the host's toolchain — is
/// never accused, and an image the operator or the context NAMED is a choice, not a fallback.
/// </para>
/// </summary>
public sealed class ToolchainImageFallbackCheck : IRunPreflightCheck
{
    private const string Lever =
        "merge the branch that carries .agentsmith/contexts/ (an open init pull request) into the default "
        + "branch, or declare stack.lang / stack.image in the context.yaml, or set sandbox.images.<lang> or "
        + "sandbox.toolchain_image for the project";

    public string Name => "toolchain-image";

    public Task<RunPreflightFinding> RunAsync(PipelineContext pipeline, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(pipeline);
        var project = pipeline.TryGet<ResolvedProject>(ContextKeys.ProjectConfig, out var p) ? p : null;
        var discoveries = pipeline.TryGet<IReadOnlyDictionary<string, RemoteContextDiscovery>>(
            ContextKeys.SandboxDiscoveries, out var d) ? d : null;
        var fellBack = Images(pipeline)
            .Where(kv => kv.Value == SandboxImageChain.GenericFallbackImage)
            .Select(kv => (Key: kv.Key, Discovery: discoveries?.GetValueOrDefault(kv.Key)))
            .Where(s => !NamedOnPurpose(project, s.Discovery))
            .Select(s => $"sandbox '{s.Key}': {Why(s.Discovery)}")
            .ToList();
        return Task.FromResult(fellBack.Count == 0
            ? RunPreflightFinding.Pass(Name, "every sandbox runs a toolchain image chosen for it")
            : RunPreflightFinding.Warn(Name,
                $"the generic image '{SandboxImageChain.GenericFallbackImage}' carries git and no language "
                + "toolchain, so nothing can be built or tested in it — " + string.Join("; ", fellBack),
                Lever));
    }

    private static IReadOnlyDictionary<string, string> Images(PipelineContext pipeline) =>
        pipeline.TryGet<IReadOnlyDictionary<string, string>>(ContextKeys.SandboxImages, out var images)
        && images is not null ? images : new Dictionary<string, string>();

    private static bool NamedOnPurpose(ResolvedProject? project, RemoteContextDiscovery? discovery) =>
        IsFallback(project?.Sandbox?.ToolchainImage)
        || IsFallback(discovery?.ToolchainImage)
        || (discovery?.Language is { } lang && project?.Sandbox?.Images?.Any(
            kv => string.Equals(kv.Key, lang, StringComparison.OrdinalIgnoreCase) && IsFallback(kv.Value)) == true);

    private static bool IsFallback(string? image) =>
        string.Equals(image?.Trim(), SandboxImageChain.GenericFallbackImage, StringComparison.Ordinal);

    private static string Why(RemoteContextDiscovery? discovery) => discovery switch
    {
        null => "no context was resolved for it",
        { Language: null } => $"context '{discovery.ContextName}' names no language — {discovery.Provenance ?? "no stack.lang declared"}",
        _ => $"context '{discovery.ContextName}' declares lang={discovery.Language}, for which no image is configured, "
            + $"declared or catalogued ({discovery.Provenance ?? "provenance not recorded"})",
    };
}
