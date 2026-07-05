using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Models.Skills;
using AgentSmith.Contracts.Services;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Prompts;

/// <summary>
/// p0179a adapter. Resolves prompt requests against the loaded master-skill
/// catalog first; falls back to the embedded prompt catalog for prompts that
/// p0179b/c will retire alongside their consumers. The name map below pins
/// the embedded-prompt-name → master-skill-name routing computed once at
/// slice a's design time; new master skills get added here together with the
/// corresponding cross-repo SKILL.md.
/// </summary>
public sealed class SkillCatalogPromptCatalog : IPromptCatalog
{
    private static readonly IReadOnlyDictionary<string, string> NameMap =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            // coding-agent-master carries the agent-execute-system body plus
            // the p0177 step-11 sub-agent guidance (spawn_agents lives in the
            // execute phase). agent-plan-system stays embedded until p0179c
            // collapses Plan/Execute/Verify into one unified body — combining
            // a JSON-returning plan prompt with a multi-turn execute prompt
            // would confuse the LLM at the plan call site.
            ["agent-execute-system"] = "coding-agent-master",
            ["project-analyzer-system"] = "project-analyzer-master",
            ["knowledge-system"] = "knowledge-master",
            ["contract-classifier-system"] = "contract-classifier-master",
            ["context-generator-system"] = "context-generator-master",
            ["context-quality-template"] = "context-generator-master",
        };

    private readonly IPromptCatalog _inner;
    private readonly ISkillLoader _skillLoader;
    private readonly ISkillsCatalogPath _catalogPath;
    private readonly ISkillBodyResolver _bodyResolver;
    private readonly ILogger<SkillCatalogPromptCatalog> _logger;

    // p0179g: skills subtree sits at {catalogRoot}/skills/. Same value as
    // ExecutePipelineUseCase.CatalogSkillsRootSubPath — both call sites pass
    // through YamlSkillLoader, which walks <root>/_masters/* for masters and
    // <root>/<category>/<skill>/SKILL.md for everything else.
    private const string CatalogSkillsRootSubPath = "skills";

    private readonly object _lock = new();
    private IReadOnlyDictionary<string, RoleSkillDefinition>? _masterCatalog;

    public SkillCatalogPromptCatalog(
        IPromptCatalog inner,
        ISkillLoader skillLoader,
        ISkillsCatalogPath catalogPath,
        ISkillBodyResolver bodyResolver,
        ILogger<SkillCatalogPromptCatalog> logger)
    {
        _inner = inner;
        _skillLoader = skillLoader;
        _catalogPath = catalogPath;
        _bodyResolver = bodyResolver;
        _logger = logger;
    }

    public string Get(string name)
    {
        if (TryGetFromMasters(name, out var masterBody))
            return masterBody;
        // p0205: NO silent embedded fallback for migrated master prompts. A
        // catalog that lacks one (missing or stale skills.version) must fail
        // loud — not quietly serve a different/older embedded copy, which masks
        // version drift between the server and the skills catalog.
        if (NameMap.TryGetValue(name, out var masterName))
            throw new InvalidOperationException(
                $"Prompt '{name}' must come from the skill catalog's '{masterName}' master, but the loaded " +
                $"catalog does not provide it. Pin a skills.version that includes it (the embedded fallback " +
                $"was removed in p0205). Point agentsmith.yml's skills source at a directory/version that has it.");
        return _inner.Get(name);
    }

    public string Render(string name, IReadOnlyDictionary<string, string> tokens)
    {
        var content = Get(name);
        foreach (var (key, value) in tokens)
        {
            content = content.Replace("{" + key + "}", value, StringComparison.Ordinal);
        }

        // Fail loud on a KNOWN master token the caller did not supply — it would otherwise
        // reach the LLM as a literal "{Token}". Braces outside the vocabulary (e.g. an
        // OpenAPI "/users/{id}" example) are deliberately ignored.
        var unbound = MasterPromptTokens.All
            .Where(token => content.Contains("{" + token + "}", StringComparison.Ordinal))
            .ToList();
        if (unbound.Count > 0)
            throw new InvalidOperationException(
                $"Prompt '{name}' has unbound master token(s): {string.Join(", ", unbound)}. " +
                "The caller must supply every token the master body references.");

        return content;
    }

    private bool TryGetFromMasters(string promptName, out string body)
    {
        body = string.Empty;
        var catalog = GetMasterCatalog();
        if (catalog is null) return false;

        // p0179a: legacy embedded-prompt name → master-skill name
        // (e.g. "agent-execute-system" → "coding-agent-master").
        if (NameMap.TryGetValue(promptName, out var masterName)
            && catalog.TryGetValue(masterName, out var mappedMaster))
        {
            body = _bodyResolver.ResolveBody(mappedMaster, SkillRole.Master);
            return true;
        }

        // p0179b/d: handler-passed master-skill name resolved directly when
        // the loaded catalog has a matching role:master entry (e.g.
        // "security-master" when wired by the security-scan pipeline).
        if (catalog.TryGetValue(promptName, out var directMaster))
        {
            body = _bodyResolver.ResolveBody(directMaster, SkillRole.Master);
            return true;
        }

        return false;
    }

    private IReadOnlyDictionary<string, RoleSkillDefinition>? GetMasterCatalog()
    {
        if (_masterCatalog is not null) return _masterCatalog;
        lock (_lock)
        {
            if (_masterCatalog is not null) return _masterCatalog;
            try
            {
                var skillsRoot = Path.Combine(_catalogPath.Root, CatalogSkillsRootSubPath);
                var all = _skillLoader.LoadRoleDefinitions(skillsRoot);
                _masterCatalog = all
                    .Where(s => string.Equals(s.Role, "master", StringComparison.Ordinal))
                    .ToDictionary(s => s.Name, s => s, StringComparer.Ordinal);
                _logger.LogDebug(
                    "SkillCatalogPromptCatalog loaded {Count} master skills", _masterCatalog.Count);
                return _masterCatalog;
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogDebug(ex, "Skill catalog not yet bootstrapped; falling back to embedded prompts");
                return null;
            }
        }
    }
}
