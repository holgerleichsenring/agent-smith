using AgentSmith.Application.Models;
using AgentSmith.Application.Services.Sandbox;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Models.Preflight;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Models;

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
/// <para>
/// 2026-11-16-f3fba: decided before any sandbox, from the chain's decision for each context the
/// sandboxes will be started for (<see cref="RunContextInventory"/>); a backend that spawns no
/// containers runs the host's toolchain and is never judged.
/// </para>
/// </summary>
public sealed class ToolchainImageFallbackCheck(
    SandboxImageChain chain, RunContextInventory inventory, SandboxContainerRuntime runtime) : IRunPreflightCheck
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

    public bool NeedsSandbox => false;

    public async Task<RunPreflightFinding> RunAsync(PipelineContext pipeline, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(pipeline);
        if (!runtime.SpawnsContainers)
            return RunPreflightFinding.Pass(Name, "the sandbox backend runs on the host's own toolchain");
        var fellBack = FellBack(pipeline, await inventory.ReadAsync(pipeline, cancellationToken));
        var blocking = fellBack.Where(s => s.Declares).ToList();
        if (blocking.Count > 0)
            return RunPreflightFinding.Fail(Name,
                $"the generic image '{SandboxImageChain.GenericFallbackImage}' carries git and no language "
                + "toolchain, and these contexts declare verify stages it cannot run — "
                + string.Join("; ", blocking.Select(s => s.Line)), BlockingLever) with { Detail = DetailOf(blocking[0]) };
        return fellBack.Count == 0
            ? RunPreflightFinding.Pass(Name, "every sandbox runs a toolchain image chosen for it")
            : RunPreflightFinding.Warn(Name,
                $"the generic image '{SandboxImageChain.GenericFallbackImage}' carries git and no language "
                + "toolchain, so nothing can be built or tested in it — "
                + string.Join("; ", fellBack.Select(s => s.Line)), Lever);
    }

    // The key the operator sets and the value it expects: an image for the context's language.
    private static EndDetail DetailOf(FallenBackContext context) =>
        new($"Context {context.Context} (lang {context.Language ?? "none"})", [context.Repo],
            context.Language is null ? "sandbox.toolchain_image" : $"sandbox.images.{context.Language}",
            "an image that carries the toolchain its verify stages call", "/config/projects");

    private List<FallenBackContext> FellBack(
        PipelineContext pipeline, IReadOnlyDictionary<string, IReadOnlyList<RemoteContextDiscovery>> byRepo)
    {
        var project = pipeline.TryGet<ResolvedProject>(ContextKeys.ProjectConfig, out var p) && p is not null
            ? p : new ResolvedProject();
        var found = new List<FallenBackContext>();
        foreach (var (repo, contexts) in byRepo)
        {
            if (contexts.Count == 0) found.Add(new FallenBackContext(repo, "-", null, $"repo '{repo}': no context was resolved for it", false));
            found.AddRange(contexts
                .Where(c => chain.Decide(project, c.Language, c.ToolchainImage).FellThrough)
                .Select(c => new FallenBackContext(repo, c.ContextName, c.Language, $"repo '{repo}': {Why(c)}", c.Verify is { Count: > 0 })));
        }
        return found;
    }

    private static string Why(RemoteContextDiscovery discovery) => discovery switch
    {
        { Language: null } => $"context '{discovery.ContextName}' names no language — {discovery.Provenance ?? "no stack.lang declared"}",
        _ => $"context '{discovery.ContextName}' declares lang={discovery.Language}, for which no image is configured, "
            + $"declared or catalogued ({discovery.Provenance ?? "provenance not recorded"})",
    } + (discovery.Verify is { Count: > 0 } stages
        ? $", and declares {stages.Count} verify stage(s) ({string.Join(", ", stages.Select(s => s.Command))})"
        : string.Empty);
}
