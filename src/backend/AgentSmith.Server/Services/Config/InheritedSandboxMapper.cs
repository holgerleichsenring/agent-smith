using AgentSmith.Application.Services.Configuration;
using AgentSmith.Contracts.Models.Configuration.Resolved;

namespace AgentSmith.Server.Services.Config;

/// <summary>
/// 2026-09-22-6968: projects the counterfactual inherited sandbox settings onto the wire.
/// Pure allow-list, like <see cref="ConfigSnapshotMapper"/> — five scalar values and their
/// provenance, nothing a secret could ride on.
/// </summary>
public static class InheritedSandboxMapper
{
    public static InheritedSandboxProjectionResponse ToResponse(IInheritedSandboxProjection projection) =>
        new(ToWire(projection.ProcessWide()),
            projection.ByProject().ToDictionary(kv => kv.Key, kv => ToWire(kv.Value), StringComparer.Ordinal));

    public static ConfigInheritedSandbox ToWire(InheritedSandboxSettings settings) => new(
        ToolchainImage: Rv(settings.ToolchainImage),
        StepTimeoutSeconds: Rv(settings.StepTimeoutSeconds),
        RunCommandTimeoutSeconds: Rv(settings.RunCommandTimeoutSeconds),
        AgentRegistry: Rv(settings.AgentRegistry),
        AgentVersion: Rv(settings.AgentVersion));

    private static ConfigResolvedValue<T> Rv<T>(ResolvedValue<T> v) =>
        new(v.Value, ResolutionSourceName.Of(v.Source));
}
