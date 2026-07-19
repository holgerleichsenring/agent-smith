using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;

namespace AgentSmith.Infrastructure.Core.Services.Configuration;

/// <summary>
/// The raw-to-typed configuration pipeline shared by every loader: resolve secret
/// + registry-token env references, apply deployment defaults, merge tracker-owned
/// workflow into each project's effective trigger, normalize pipelines, fill skills
/// defaults, then materialize catalog references via <see cref="ConfigCatalogResolver"/>.
/// p0349: extracted so the file loader and the DB loader run the IDENTICAL pipeline
/// over a <see cref="RawAgentSmithConfig"/> regardless of where the raw shape came from.
/// </summary>
public sealed class RawConfigMaterializer(
    ProjectConfigNormalizer normalizer,
    EffectiveTriggerBuilder effectiveTriggers,
    DeploymentDefaultsApplier deploymentDefaults,
    ConfigCatalogResolver resolver,
    IAgentSmithPaths paths)
{
    public AgentSmithConfig Materialize(RawAgentSmithConfig raw)
    {
        ResolveSecrets(raw);
        ResolveRegistryTokens(raw);
        deploymentDefaults.Apply(raw);
        ApplyEffectiveTriggers(raw);
        NormalizeProjects(raw);
        FillSkillsDefaults(raw);
        return resolver.Resolve(raw);
    }

    private void ApplyEffectiveTriggers(RawAgentSmithConfig raw)
    {
        foreach (var (name, project) in raw.Projects)
        {
            raw.Trackers.TryGetValue(project.Tracker, out var tracker);
            effectiveTriggers.Apply(name, project, tracker);
        }
    }

    private void NormalizeProjects(RawAgentSmithConfig raw)
    {
        foreach (var (name, project) in raw.Projects)
            normalizer.Normalize(name, project);
    }

    private void FillSkillsDefaults(RawAgentSmithConfig raw)
    {
        if (string.IsNullOrWhiteSpace(raw.Skills.CacheDir))
            raw.Skills.CacheDir = paths.SkillsCatalogRoot;
        InferSkillsSource(raw.Skills);
    }

    // p0325: skills ship embedded; an absent/blank skills block resolves to the
    // embedded catalog. Explicit config wins — a set source or version is honored.
    private static void InferSkillsSource(SkillsConfig skills)
    {
        if (skills.Source != SkillsSourceMode.Default || !string.IsNullOrWhiteSpace(skills.Version))
            return;

        skills.Source = !string.IsNullOrWhiteSpace(skills.Path) ? SkillsSourceMode.Path
            : !string.IsNullOrWhiteSpace(skills.Url) ? SkillsSourceMode.Url
            : SkillsSourceMode.Embedded;
    }

    // p0191: registry tokens reference secrets via ${name}; substitute them after
    // ResolveSecrets has replaced the secrets-dict values with env-var contents.
    private static void ResolveRegistryTokens(RawAgentSmithConfig raw)
    {
        foreach (var entry in raw.Registries)
            entry.Token = ResolveSecretReference(entry.Token, raw.Secrets);
    }

    private static string ResolveSecretReference(string value, IReadOnlyDictionary<string, string> secrets)
    {
        if (!value.StartsWith("${") || !value.EndsWith("}")) return value;
        var key = value[2..^1];
        return secrets.TryGetValue(key, out var resolved) ? resolved : string.Empty;
    }

    private static void ResolveSecrets(RawAgentSmithConfig raw)
    {
        var resolved = new Dictionary<string, string>();
        foreach (var (key, value) in raw.Secrets)
            resolved[key] = ResolveEnvironmentVariable(value);
        raw.Secrets = resolved;
    }

    private static string ResolveEnvironmentVariable(string value)
    {
        if (!value.StartsWith("${") || !value.EndsWith("}")) return value;
        var varName = value[2..^1];
        return Environment.GetEnvironmentVariable(varName) ?? string.Empty;
    }
}
