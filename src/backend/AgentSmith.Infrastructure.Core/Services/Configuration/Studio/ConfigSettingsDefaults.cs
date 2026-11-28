namespace AgentSmith.Infrastructure.Core.Services.Configuration.Studio;

/// <summary>
/// 2026-11-28-9bd1: what a settings singleton holds when nobody set it — the same read as
/// <see cref="ConfigSettingsAccess.Read"/>, over an empty config. The studio's group pages
/// count a section's differences from it. The value depends on no store, so it is read here
/// once rather than by each store.
/// </summary>
public static class ConfigSettingsDefaults
{
    /// <summary>The model default of one settings singleton (serialized camelCase on the wire).</summary>
    public static object Read(string type) => ConfigSettingsAccess.Read(new RawAgentSmithConfig(), type);
}
