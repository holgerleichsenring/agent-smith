using AgentSmith.Contracts.Models.Configuration.Resolved;

namespace AgentSmith.Server.Services.Config;

/// <summary>
/// 2026-09-22-6c46: the wire name of the layer that answers a sandbox's cpu/memory. A
/// closed vocabulary, like <see cref="ResolutionSourceName"/> — the dashboard turns each
/// name into the sentence that says WHICH layer would answer, which is the one honest
/// thing a control with four layers behind it can say.
/// </summary>
public static class SandboxResourceLayerName
{
    public static string Of(SandboxResourceLayer layer) => layer switch
    {
        SandboxResourceLayer.ProjectOverride => "project-override",
        SandboxResourceLayer.LightProfile => "light-profile",
        SandboxResourceLayer.ContextDocument => "context-document",
        _ => "global-default",
    };
}
