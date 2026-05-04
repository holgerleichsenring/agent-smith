using AgentSmith.Contracts.Services;

namespace AgentSmith.Infrastructure.Core.Services;

/// <summary>
/// Resolves the effective SKILL.md path for the active provider. Honors
/// <c>SKILL.&lt;provider&gt;.md</c> overrides in the skill directory; falls back to
/// the base <c>SKILL.md</c> when no override exists or no provider is active.
/// </summary>
public sealed class ProviderOverrideResolver(IActiveProviderResolver activeProviderResolver) : IProviderOverrideResolver
{
    private const string BaseFileName = "SKILL.md";

    public ProviderOverridePaths Resolve(string skillDirectory)
    {
        var basePath = Path.Combine(skillDirectory, BaseFileName);
        var provider = activeProviderResolver.GetActiveProvider();
        if (string.IsNullOrEmpty(provider))
            return new ProviderOverridePaths(basePath, null);

        var overridePath = Path.Combine(skillDirectory, $"SKILL.{provider}.md");
        return File.Exists(overridePath)
            ? new ProviderOverridePaths(overridePath, basePath)
            : new ProviderOverridePaths(basePath, null);
    }
}
