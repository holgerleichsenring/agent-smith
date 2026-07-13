using AgentSmith.Application.Prompts;
using AgentSmith.Contracts.Models.Preflight;
using AgentSmith.Contracts.Services;

namespace AgentSmith.Application.Services.Preflight.Checks;

/// <summary>
/// p0324: materializes the pinned skill catalog and loads every skill through the
/// real loader — the two historic silent killers here were pin drift (a stale or
/// missing skills.version serving old masters) and a master description over the
/// loader's 200-char limit, which used to drop the master silently and kill the run
/// later with 'Prompt resource not found'. Verifies every master the prompt routing
/// requires is actually present in the loaded catalog.
/// </summary>
public sealed class SkillsCatalogCheck(
    IPreflightConfigSource configSource,
    ISkillsCatalogResolver catalogResolver,
    ISkillLoader skillLoader) : IPreflightCheck
{
    private const string SkillsSubdirectory = "skills";

    public string Name => "skills-catalog";

    public string Category => "skills";

    public async Task<PreflightCheckResult> RunAsync(CancellationToken cancellationToken)
    {
        var config = configSource.Resolve().Config;
        if (config is null)
            return PreflightCheckResult.Skip("agentsmith.yml failed to load — see config-schema");

        string root;
        string version;
        try
        {
            var resolution = await catalogResolver.EnsureResolvedAsync(config.Skills, cancellationToken);
            root = resolution.Root;
            version = $"{resolution.Version} ({(resolution.FromCache ? "cached" : "freshly pulled")})";
        }
        catch (Exception ex)
        {
            return PreflightCheckResult.Fail(
                $"skill catalog failed to resolve: {ex.Message}",
                "Check skills.version — the pinned release tag must exist at the skills source (or fix "
                + "skills.path/url). A wrong pin is silent drift: the server keeps running old masters.");
        }

        try
        {
            var skills = skillLoader.LoadRoleDefinitions(Path.Combine(root, SkillsSubdirectory));
            var masters = skills
                .Where(s => string.Equals(s.Role, "master", StringComparison.Ordinal))
                .Select(s => s.Name)
                .ToHashSet(StringComparer.Ordinal);
            var missing = SkillCatalogPromptCatalog.RequiredMasterSkills
                .Where(m => !masters.Contains(m))
                .ToList();
            if (missing.Count > 0)
                return PreflightCheckResult.Fail(
                    $"catalog {version} lacks required master(s): {string.Join(", ", missing)}",
                    "Pin a skills.version that ships these masters — the embedded fallback was removed "
                    + "in p0205, so a run needing them dies with 'Prompt resource not found'.");

            return PreflightCheckResult.Pass(
                $"catalog {version}: {skills.Count} skill(s), {masters.Count} master(s), "
                + "all required masters present");
        }
        catch (Exception ex)
        {
            return PreflightCheckResult.Fail(
                $"skill catalog failed to load: {ex.Message}",
                "Fix the SKILL.md named above. Master descriptions must stay at or under 200 characters — "
                + "an over-limit description drops the master at load time and the run dies later with "
                + "'Prompt resource not found' (the v3.16.0 incident).");
        }
    }
}
