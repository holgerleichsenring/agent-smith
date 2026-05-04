namespace AgentSmith.Contracts.Services;

/// <summary>
/// Resolves which SKILL.md file to use for the active provider. When a
/// provider-specific override <c>SKILL.&lt;provider&gt;.md</c> exists in the
/// skill directory, returns its path as <see cref="ProviderOverridePaths.EffectivePath"/>
/// and the base SKILL.md path as <see cref="ProviderOverridePaths.BasePath"/>.
/// When no override exists, returns the base path with <see cref="ProviderOverridePaths.BasePath"/>
/// set to null. The parser uses BasePath to validate that name and roles_supported match.
/// </summary>
public interface IProviderOverrideResolver
{
    ProviderOverridePaths Resolve(string skillDirectory);
}

public sealed record ProviderOverridePaths(string EffectivePath, string? BasePath);
