using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Models.Preflight;
using AgentSmith.Contracts.Services;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Preflight;

/// <summary>
/// p0324: loads agentsmith.yml once (lazily) for the whole preflight run and turns a
/// parse/IO failure into a result instead of an exception — the config-schema check
/// reports it, every other check skips on it.
/// </summary>
public sealed class PreflightConfigSource(
    IConfigurationLoader loader,
    ServerContext context,
    ILogger<PreflightConfigSource> logger) : IPreflightConfigSource
{
    private readonly object _lock = new();
    private PreflightConfigResult? _cached;

    public PreflightConfigResult Resolve()
    {
        lock (_lock)
        {
            return _cached ??= Load();
        }
    }

    private PreflightConfigResult Load()
    {
        try
        {
            return new PreflightConfigResult(loader.LoadConfig(context.ConfigPath), context.ConfigPath, null);
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "Preflight config load failed for {Path}", context.ConfigPath);
            return new PreflightConfigResult(null, context.ConfigPath, ex.Message);
        }
    }
}
