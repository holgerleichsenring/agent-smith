using AgentSmith.Application.Services.Builders;
using AgentSmith.Application.Services.Sandbox;
using AgentSmith.Contracts.Events;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Sandbox;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AgentSmith.Application.Services.Browser;

/// <summary>
/// 2026-10-01-283de: the browser is a tool runtime — a sandbox of its own, with no repository,
/// spawned by whichever backend spawns every other sandbox. Its spec is the project's, with the
/// browser image as toolchain, the browser profile's resources and a readiness window that a cold
/// pull of a browser image fits in. It is held per conversation under one key, so a design
/// conversation pays one spawn however many renders it asks for. The server never runs a browser:
/// a backend that spawns no container has no browser runtime, and the opener says so.
/// </summary>
public sealed class BrowserSandboxOpener(
    SandboxContainerRuntime runtime,
    ISandboxFactory sandboxFactory,
    SandboxSpecBuilder specBuilder,
    IBrowserImageResolver images,
    IOptions<SandboxGlobalConfig> config,
    IRunContextAccessor runContext,
    IHeldSandboxRegister holds,
    ILogger<BrowserSandboxOpener> logger)
{
    internal const string HeldName = "browser";
    internal const int ReadinessSeconds = 600;
    internal const string NoRuntime =
        "no browser runtime exists here: this server runs sandbox steps in-process and spawns no "
        + "container, so it cannot start the browser image. Rendering needs the Docker or Kubernetes backend.";

    /// <summary>
    /// The conversation's browser sandbox, or why there is none. 2026-10-01-283df: with no
    /// conversation — a run — one spawned for this render and disposed after it.
    /// </summary>
    public async Task<(BrowserSandboxLease? Lease, string? Refusal)> OpenAsync(
        ResolvedProject project, string? conversationId, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(project);
        if (!runtime.SpawnsContainers) return (null, NoRuntime);
        if (conversationId is null) return (new BrowserSandboxLease(await SpawnAsync(project, null, ct), null), null);
        var hold = new SourceScopeHold(holds, conversationId, HeldName, null, logger);
        var (sandbox, _) = await hold.OpenAsync(
            (conversation, c) => SpawnAsync(project, conversation, c),
            (_, _) => Task.FromResult(string.Empty), ct);
        return (new BrowserSandboxLease(sandbox, hold), null);
    }

    private Task<ISandbox> SpawnAsync(ResolvedProject project, string? conversationId, CancellationToken ct)
    {
        var spec = specBuilder.Build(project, language: null, pipelineName: null) with
        {
            ToolchainImage = images.Resolve(project),
            Resources = config.Value.Browser.Resources,
            TimeoutSeconds = ReadinessSeconds,
            RunId = runContext.CurrentRunId,
            ConversationId = conversationId,
        };
        logger.LogInformation("Spawning the browser sandbox ({Image}) for {Owner}",
            spec.ToolchainImage, conversationId is null ? $"run {spec.RunId}" : $"conversation {conversationId}");
        return sandboxFactory.CreateAsync(spec, ct);
    }
}
