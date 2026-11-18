using System.Text.Json;
using AgentSmith.Contracts.Models.ConfigStudio;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Exceptions;

namespace AgentSmith.Application.Services.Admin;

/// <summary>
/// 2026-11-17-f3a8c: the kinds the admin assistant may change, by the Config studio's route names, and the settings
/// singletons as settings/&lt;type&gt;. The role mapping is the access surface's (access.write) and is refused here.
/// </summary>
public sealed class ConfigEntityKinds
{
    public const string SettingsPrefix = "settings/";
    private const string RoleMapping = "role_mapping";

    private static readonly Dictionary<string, ConfigEntityKind> Entities = new(StringComparer.OrdinalIgnoreCase)
    {
        ["agents"] = ConfigEntityKind.For<AgentEntity>(s => s.GetAgents(), e => e.Id, (e, id) => e with { Id = id }, (s, e, b) => s.UpsertAgent(e, b), (s, id, b) => s.DeleteAgent(id, b)),
        ["trackers"] = ConfigEntityKind.For<TrackerEntity>(s => s.GetTrackers(), e => e.Id, (e, id) => e with { Id = id }, (s, e, b) => s.UpsertTracker(e, b), (s, id, b) => s.DeleteTracker(id, b)),
        ["repos"] = ConfigEntityKind.For<RepoEntity>(s => s.GetRepos(), e => e.Id, (e, id) => e with { Id = id }, (s, e, b) => s.UpsertRepo(e, b), (s, id, b) => s.DeleteRepo(id, b)),
        ["projects"] = ConfigEntityKind.For<ProjectEntity>(s => s.GetProjects(), e => e.Id, (e, id) => e with { Id = id }, (s, e, b) => s.UpsertProject(e, b), (s, id, b) => s.DeleteProject(id, b)),
        ["mcp-servers"] = ConfigEntityKind.For<McpServerEntity>(s => s.GetMcpServers(), e => e.Id, (e, id) => e with { Id = id }, (s, e, b) => s.UpsertMcpServer(e, b), (s, id, b) => s.DeleteMcpServer(id, b)),
        ["secrets"] = ConfigEntityKind.For<SecretEntity>(s => s.GetSecrets(), e => e.Id, (e, id) => e with { Id = id }, (s, e, b) => s.UpsertSecret(e, b), (s, id, b) => s.DeleteSecret(id, b)),
        ["connections"] = ConfigEntityKind.For<ConnectionEntity>(s => s.GetConnections(), e => e.Id, (e, id) => e with { Id = id }, (s, e, b) => s.UpsertConnection(e, b), (s, id, b) => s.DeleteConnection(id, b)),
        ["design-sources"] = ConfigEntityKind.For<DesignSourceEntity>(s => s.GetDesignSources(), e => e.Id, (e, id) => e with { Id = id }, (s, e, b) => s.UpsertDesignSource(e, b), (s, id, b) => s.DeleteDesignSource(id, b)),
    };

    /// <summary>Every kind name the assistant may use.</summary>
    public static IReadOnlyList<string> Names(IConfigStore store) =>
        [.. Entities.Keys, .. store.SettingTypes.Where(t => t != RoleMapping).Select(t => SettingsPrefix + t)];

    /// <summary>The entity or settings value as a draft shows it; null when the store holds none.</summary>
    public string? Read(IConfigStore store, string kind, string id) =>
        ConfigEntityJson.Render(Setting(kind) is { } type ? store.GetSetting(type) : Of(kind).Read(store, id));

    public void Upsert(IConfigStore store, string kind, string id, string document, ChangeAttribution by)
    {
        var parsed = JsonDocument.Parse(document).RootElement;
        if (Setting(kind) is { } type) store.SaveSetting(type, parsed, by);
        else Of(kind).Upsert(store, id, parsed, by);
    }

    public void Delete(IConfigStore store, string kind, string id, ChangeAttribution by)
    {
        if (Setting(kind) is not null) throw new ConfigurationException($"A setting ('{kind}') is changed, never deleted.");
        Of(kind).Delete(store, id, by);
    }

    private static ConfigEntityKind Of(string kind) => Entities.GetValueOrDefault(kind)
        ?? throw new ConfigurationException($"'{kind}' is not a configuration kind. Known: {string.Join(", ", Entities.Keys)}, settings/<type>.");

    private static string? Setting(string kind)
    {
        if (!kind.StartsWith(SettingsPrefix, StringComparison.OrdinalIgnoreCase)) return null;
        var type = kind[SettingsPrefix.Length..];
        return type == RoleMapping
            ? throw new ConfigurationException("The role mapping is changed on the access page (access.write), not here.")
            : type;
    }
}
