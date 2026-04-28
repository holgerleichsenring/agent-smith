using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;

namespace AgentSmith.Tests.TestSupport;

/// <summary>
/// Test stub for <see cref="ISkillsCatalogPath"/> that returns a fixed root,
/// or empty string when none is provided so tests can use absolute paths
/// directly without exercising catalog resolution.
/// </summary>
internal sealed class StubSkillsCatalogPath(string root = "") : ISkillsCatalogPath
{
    public string Root { get; } = root;
}

/// <summary>
/// Test stub for <see cref="ISkillsCatalogResolver"/> — never resolves anything.
/// Tests that don't exercise catalog resolution wire this in.
/// </summary>
internal sealed class StubSkillsCatalogResolver : ISkillsCatalogResolver
{
    public Task EnsureResolvedAsync(SkillsConfig config, CancellationToken cancellationToken)
        => Task.CompletedTask;
}
