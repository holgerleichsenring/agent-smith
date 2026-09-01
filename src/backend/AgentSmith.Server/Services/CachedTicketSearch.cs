using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Services;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Server.Services;

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

    public async Task<IReadOnlyList<(string Id, string Title)>> SearchAsync(
        string project, string? query, CancellationToken ct)
    {
        var allTickets = await GetOrLoadTicketsAsync(project, ct);

        if (string.IsNullOrWhiteSpace(query))
            return allTickets;

        return allTickets
            .Where(t =>
                t.Title.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                t.Id.Contains(query, StringComparison.OrdinalIgnoreCase))
            .ToList();
    }

    private async Task<IReadOnlyList<(string Id, string Title)>> GetOrLoadTicketsAsync(
        string project, CancellationToken ct)
    {
        var cacheKey = $"{CacheKeyPrefix}{project}";

        if (cache.TryGetValue<IReadOnlyList<(string Id, string Title)>>(cacheKey, out var cached) && cached is not null)
            return cached;

        var config = configLoader.LoadConfig(DispatcherDefaults.ConfigPath);
        if (!config.Projects.TryGetValue(project, out var projectConfig))
        {
            logger.LogWarning("Project {Project} not found in configuration", project);
            return [];
        }

        try
        {
            var ticketProvider = ticketFactory.Create(projectConfig.Tracker);
            var tickets = await ticketProvider.ListOpenAsync(ct);

            // 2026-09-01-ba47: the identifier is whatever the tracker wrote. Parsing it to
            // an int and dropping everything that failed emptied every Jira project's list.
            var result = tickets
                .Select(t => (Id: t.Id.Value, t.Title))
                .Where(t => !string.IsNullOrWhiteSpace(t.Id))
                .ToList() as IReadOnlyList<(string Id, string Title)>;

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
