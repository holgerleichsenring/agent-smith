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
    IAgentSmithPaths paths,
    IStartupFindings? findings = null,
    ConfigSecretReferences? secretReferences = null)
{
    private readonly IStartupFindings _findings = findings ?? new StartupFindings();
    private readonly ConfigSecretReferences _references =
        secretReferences ?? new ConfigSecretReferences(Environment.GetEnvironmentVariable);
    private readonly List<StartupFinding> _unmaterializable = [];

    /// <summary>
    /// p0391b: what the LAST materialization could not resolve. A configuration whose
    /// references do not resolve is not a degraded configuration, it is an absent one for
    /// the projects concerned — the one-shot file loader turns these into its exit code,
    /// the server records them and stays up.
    /// </summary>
    public IReadOnlyList<StartupFinding> LastResolutionFindings { get; private set; } = [];

    public AgentSmithConfig Materialize(RawAgentSmithConfig raw)
    {
        // p0391a: every load republishes the configuration findings, so a fault an
        // operator has fixed stops being reported without a restart.
        _findings.Clear(StartupSubsystems.Configuration);
        _unmaterializable.Clear();
        ResolveSecrets(raw);
        ResolveSecretReferences(raw);
        deploymentDefaults.Apply(raw);
        ApplyEffectiveTriggers(raw);
        NormalizeProjects(raw);
        FillSkillsDefaults(raw);
        var config = resolver.Resolve(raw);
        LastResolutionFindings = [.. _unmaterializable, .. resolver.LastFindings];
        return config;
    }

    // p0391b: an unknown resolution shorthand key used to throw out of here, and the
    // server's loader turned that into "the whole configuration is unusable" — one typo
    // in one project silenced every other project. It is now that project's finding.
    private void ApplyEffectiveTriggers(RawAgentSmithConfig raw)
    {
        foreach (var (name, project) in raw.Projects)
        {
            raw.Trackers.TryGetValue(project.Tracker, out var tracker);
            try
            {
                effectiveTriggers.Apply(name, project, tracker);
            }
            catch (Domain.Exceptions.ConfigurationException ex)
            {
                var finding = ProjectFindings.Blocking(name, "resolution", ex.Message);
                _unmaterializable.Add(finding);
                _findings.Record(finding);
            }
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
    // p0506: a project's jira_trigger.secret carries the same reference shape — the
    // shipped example writes ${JIRA_WEBHOOK_SECRET} — and was substituted by nothing,
    // so the verifier compared deliveries against the literal placeholder.
    private void ResolveSecretReferences(RawAgentSmithConfig raw)
    {
        foreach (var entry in raw.Registries)
            entry.Token = _references.Resolve(entry.Token, raw.Secrets);
        foreach (var project in raw.Projects.Values)
            if (project.JiraTrigger?.Secret is { } secret)
                project.JiraTrigger.Secret = _references.Resolve(secret, raw.Secrets);
    }

    private void ResolveSecrets(RawAgentSmithConfig raw)
    {
        var resolved = new Dictionary<string, string>();
        foreach (var (key, value) in raw.Secrets)
            resolved[key] = _references.ResolveFromEnvironment(value);
        raw.Secrets = resolved;
    }
}
