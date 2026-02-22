using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Services;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Dispatcher.Services;

/// <summary>
/// Cached ticket search that loads all open tickets per project (cached 60s)
/// and filters in-memory by search query. Avoids hammering ticket APIs on
/// every keystroke in the Slack external_select dropdown.
/// </summary>
internal sealed class CachedTicketSearch(
    IConfigurationLoader configLoader,
    ITicketProviderFactory ticketFactory,
    IMemoryCache cache,
    ILogger<CachedTicketSearch> logger)
{
    private const int CacheTtlSeconds = 60;
    private const string CacheKeyPrefix = "tickets:";

    public async Task<IReadOnlyList<(int Id, string Title)>> SearchAsync(
        string project, string? query, CancellationToken ct = default)
    {
        var allTickets = await GetOrLoadTicketsAsync(project, ct);

        if (string.IsNullOrWhiteSpace(query))
            return allTickets;

        return allTickets
            .Where(t =>
                t.Title.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                t.Id.ToString().Contains(query, StringComparison.Ordinal))
            .ToList();
    }

    private async Task<IReadOnlyList<(int Id, string Title)>> GetOrLoadTicketsAsync(
        string project, CancellationToken ct)
    {
        var cacheKey = $"{CacheKeyPrefix}{project}";

        if (cache.TryGetValue<IReadOnlyList<(int Id, string Title)>>(cacheKey, out var cached) && cached is not null)
            return cached;

        var config = configLoader.LoadConfig(DispatcherDefaults.ConfigPath);
        if (!config.Projects.TryGetValue(project, out var projectConfig))
        {
            logger.LogWarning("Project {Project} not found in configuration", project);
            return [];
        }

        try
        {
            var ticketProvider = ticketFactory.Create(projectConfig.Tickets);
            var tickets = await ticketProvider.ListOpenAsync(ct);

            var result = tickets
                .Select(t => (Id: int.TryParse(t.Id.Value, out var id) ? id : 0, t.Title))
                .Where(t => t.Id > 0)
                .ToList() as IReadOnlyList<(int Id, string Title)>;

            cache.Set(cacheKey, result, TimeSpan.FromSeconds(CacheTtlSeconds));

            logger.LogInformation("Loaded {Count} tickets for {Project} (cached {Ttl}s)",
                result.Count, project, CacheTtlSeconds);

            return result;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to load tickets for {Project}", project);
            return [];
        }
    }
}
