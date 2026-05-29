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
        return _inner.Get(name);
    }

    public string Render(string name, IReadOnlyDictionary<string, string> tokens)
    {
        var content = Get(name);
        foreach (var (key, value) in tokens)
        {
            content = content.Replace("{" + key + "}", value, StringComparison.Ordinal);
        }
        return content;
    }

    private bool TryGetFromMasters(string promptName, out string body)
    {
        body = string.Empty;
        if (!NameMap.TryGetValue(promptName, out var masterName))
            return false;

        var catalog = GetMasterCatalog();
        if (catalog is null || !catalog.TryGetValue(masterName, out var master))
            return false;

        body = _bodyResolver.ResolveBody(master, SkillRole.Master);
        return true;
    }

    private IReadOnlyDictionary<string, RoleSkillDefinition>? GetMasterCatalog()
    {
        if (_masterCatalog is not null) return _masterCatalog;
        lock (_lock)
        {
            if (_masterCatalog is not null) return _masterCatalog;
            try
            {
                var all = _skillLoader.LoadRoleDefinitions(_catalogPath.Root);
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
