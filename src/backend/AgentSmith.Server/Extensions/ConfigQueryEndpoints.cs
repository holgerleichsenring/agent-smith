using AgentSmith.Application.Services.Configuration;
using AgentSmith.Application.Services.Sandbox;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;
using AgentSmith.Server.Security;
using AgentSmith.Server.Services.Config;
using Microsoft.Extensions.Options;

namespace AgentSmith.Server.Extensions;

/// <summary>
/// p0266: the dashboard's READ surface for the resolved agent-smith config —
/// "how the system is wired" (projects → repos/trackers/agent/pipelines +
/// global defaults), the config-time complement to the per-run topology. Maps
/// the configuration as loaded per request through
/// <see cref="ConfigSnapshotMapper"/>, which redacts every secret-bearing field.
/// Mapped only inside Program.cs's <c>AGENTSMITH_UI_API_ENABLED</c> block (like
/// the run + catalog endpoints), so a dashboard-less deployment never exposes it.
/// </summary>
internal static class ConfigQueryEndpoints
{
    internal static WebApplication MapConfigQueryEndpoints(this WebApplication app)
    {
        // p0345c: the loader's LastRead + the file's mtime ride along as drift
        // facts (configPath / fileModifiedAt / lastReadAt) for the config-reads
        // story — "is what runs what you configured".
        app.MapGet("/api/config", Snapshot).Needs(Permissions.ConfigRead);

        // 2026-09-22-6968: what each project would inherit if its own sandbox block were
        // empty. Deliberately NOT the snapshot above: that one answers what a run of this
        // project gets, which for a project that HAS an override is the override — a
        // placeholder built from it would show the operator their own value back.
        app.MapGet("/api/config/inherited-sandbox", (IInheritedSandboxProjection projection) =>
            Results.Ok(InheritedSandboxMapper.ToResponse(projection)))
           .Needs(Permissions.ConfigRead);

        // 2026-10-12-6bfab: the sandbox image boundary the NEXT stack.image decision uses,
        // read the way that decision reads it — so the Sandbox settings show one answer.
        app.MapGet("/api/config/sandbox/image-trust", (IImageRegistryTrust trust) =>
            Results.Ok(ImageTrustResponse.Map(trust.InForce())))
           .Needs(Permissions.ConfigRead);
        return app;
    }

    // 2026-10-29-6bd3d: loaded per request, not the AgentSmithConfig singleton — that one is the
    // configuration the server booted with, and showed a tracker deleted twenty minutes earlier. The
    // resolution pass is built over the same load, because the registered one holds the boot config too.
    internal static IResult Snapshot(
        IConfigurationLoader loader, ServerContext serverContext, IOptions<SandboxGlobalConfig> sandboxGlobal,
        ISandboxResourceResolver resources, IAgentImageResolver agentImages)
    {
        // Taken BEFORE this request's own read, which would otherwise always report "read just now".
        var lastRead = loader.LastRead;
        var config = loader.LoadConfig(serverContext.ConfigPath);
        var resolver = new ConfigResolutionPass(sandboxGlobal, resources, agentImages, config);
        return Results.Ok(ConfigSnapshotMapper.ToSnapshot(config, resolver, lastRead));
    }
}
