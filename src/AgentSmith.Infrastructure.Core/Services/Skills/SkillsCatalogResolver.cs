using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Infrastructure.Core.Services.Skills;

/// <summary>
/// Process-wide guard that resolves the skill catalog at most once. The first
/// caller (CLI command, server bootstrap, autonomous run) runs the dispatched
/// source handler; subsequent callers see the result via <see cref="ISkillsCatalogPath"/>.
/// </summary>
public sealed class SkillsCatalogResolver(
    IEnumerable<ISkillsSourceHandler> handlers,
    SkillsCatalogPath catalogPath,
    ILogger<SkillsCatalogResolver> logger) : ISkillsCatalogResolver
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private bool _resolved;

    public async Task EnsureResolvedAsync(SkillsConfig config, CancellationToken cancellationToken)
    {
        if (_resolved)
            return;

        await _gate.WaitAsync(cancellationToken);
        try
        {
            if (_resolved)
                return;

            var handler = handlers.FirstOrDefault(h => h.Mode == config.Source)
                ?? throw new InvalidOperationException(
                    $"No SkillsSourceHandler registered for source '{config.Source}'");

            logger.LogInformation("Resolving skill catalog (source: {Source})", config.Source);
            var root = await handler.ResolveAsync(config, cancellationToken);
            catalogPath.Set(root);
            logger.LogInformation("Skill catalog ready at {Root}", root);
            _resolved = true;
        }
        finally
        {
            _gate.Release();
        }
    }
}
