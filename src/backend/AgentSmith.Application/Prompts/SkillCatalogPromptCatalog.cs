using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Models.Skills;
using AgentSmith.Contracts.Services;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Prompts;

/// <summary>
/// Resolves a prompt request against the source <see cref="PromptOwnership"/>
/// declares for that name: a catalog-owned name comes from its master skill or
/// fails loud (p0205), an embedded-owned name comes from the embedded catalog and
/// never consults the masters, and an undeclared name is a handler-passed
/// master-skill name resolved directly from the loaded catalog.
/// </summary>
public sealed class SkillCatalogPromptCatalog : IPromptCatalog
{
    private readonly IPromptCatalog _inner;
    private readonly ISkillLoader _skillLoader;
    private readonly ISkillsCatalogPath _catalogPath;
    private readonly ISkillBodyResolver _bodyResolver;
    private readonly ILogger<SkillCatalogPromptCatalog> _logger;

    // p0179g: skills subtree sits at {catalogRoot}/skills/. Same value as
    // ConceptVocabularyLoader.CatalogSkillsRootSubPath — both call sites pass
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
        if (!PromptOwnership.TryGetOwner(name, out var owner))
            return TryGetMasterBody(name, out var direct) ? direct : _inner.Get(name);

        if (owner.Source == PromptSource.EmbeddedResource)
            return _inner.Get(name);

        if (TryGetMasterBody(owner.MasterSkillName, out var body)) return body;
        throw new InvalidOperationException(
            $"Prompt '{name}' must come from the skill catalog's '{owner.MasterSkillName}' master, " +
            $"but the loaded catalog does not provide it. Pin a skills.version that includes it (the " +
            $"embedded fallback was removed in p0205). Point agentsmith.yml's skills source at a " +
            $"directory/version that has it.");
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

    private bool TryGetMasterBody(string masterName, out string body)
    {
        body = string.Empty;
        var catalog = GetMasterCatalog();
        if (catalog is null || !catalog.TryGetValue(masterName, out var master)) return false;
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
