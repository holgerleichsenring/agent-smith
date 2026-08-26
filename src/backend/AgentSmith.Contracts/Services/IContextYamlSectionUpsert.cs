using AgentSmith.Contracts.Models.Configuration;

namespace AgentSmith.Contracts.Services;

/// <summary>
/// 2026-08-26-364f: merges the top-level sections a written document states into the
/// context.yaml already on disk. The typed <c>ContextYamlDocument</c> models meta,
/// stack, arch, quality, behavior, prerequisites and registry_auth — a whole-file
/// write therefore deleted state, methodology, integrations, data and decisions on
/// every re-init.
/// </summary>
public interface IContextYamlSectionUpsert
{
    /// <summary>
    /// Replaces each top-level key <paramref name="documentYaml"/> states in
    /// <paramref name="existingYaml"/> and leaves every other key as it was.
    /// An existing file that does not parse is reported, never replaced.
    /// </summary>
    ContextYamlUpsertResult Upsert(string? existingYaml, string documentYaml);
}
