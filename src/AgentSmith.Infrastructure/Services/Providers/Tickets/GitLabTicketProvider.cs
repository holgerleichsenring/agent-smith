using System.Net.Http.Json;
using System.Text.Json;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Providers;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Exceptions;
using AgentSmith.Domain.Models;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Infrastructure.Services.Providers.Tickets;

/// <summary>
/// Fetches issues from GitLab using the REST API v4.
/// </summary>
public sealed class GitLabTicketProvider : ITicketProvider
{
    private readonly string _baseUrl;
    private readonly string _projectPath;
    private readonly string _privateToken;
    private readonly HttpClient _httpClient;
    private readonly GitLabAttachmentLoader _attachmentLoader;
    private readonly ILogger<GitLabTicketProvider> _logger;

    public string ProviderType => "GitLab";

    public GitLabTicketProvider(
        string baseUrl, string projectPath, string privateToken,
        HttpClient httpClient, GitLabAttachmentLoader attachmentLoader,
        ILogger<GitLabTicketProvider> logger)
    {
        _baseUrl = baseUrl.TrimEnd('/');
        _projectPath = projectPath;
        _privateToken = privateToken;
        _httpClient = httpClient;
        _attachmentLoader = attachmentLoader;
        _logger = logger;
    }

    public async Task<Ticket> GetTicketAsync(
        TicketId ticketId, CancellationToken cancellationToken)
    {
        var url = $"{_baseUrl}/api/v4/projects/{_projectPath}/issues/{ticketId.Value}";

        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Add("PRIVATE-TOKEN", _privateToken);

        using var response = await _httpClient.SendAsync(request, cancellationToken);

        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            throw new TicketNotFoundException(ticketId);

        await response.EnsureSuccessWithBodyAsync(cancellationToken);

        using var json = await JsonDocument.ParseAsync(
            await response.Content.ReadAsStreamAsync(cancellationToken), cancellationToken: cancellationToken);

        var root = json.RootElement;

        var title = root.GetProperty("title").GetString() ?? string.Empty;
        var description = root.GetProperty("description").GetString() ?? string.Empty;
        var state = root.GetProperty("state").GetString() ?? string.Empty;

        return new Ticket(ticketId, title, description, null, state, "GitLab");
    }

    public async Task<IReadOnlyList<Ticket>> ListByLifecycleStatusAsync(
        TicketLifecycleStatus status, CancellationToken cancellationToken)
    {
        var rawLabel = LifecycleLabels.For(status);
        _logger.LogInformation(
            "GitLab ListByLifecycleStatus: project={Project} status={Status} (label '{Label}')",
            _projectPath, status, rawLabel);
        try
        {
            var label = Uri.EscapeDataString(rawLabel);
            var url = $"{_baseUrl}/api/v4/projects/{_projectPath}/issues?labels={label}&state=opened&per_page=100";

            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Add("PRIVATE-TOKEN", _privateToken);

            using var response = await _httpClient.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "GitLab ListByLifecycleStatus: HTTP {Status} for project={Project}",
                    (int)response.StatusCode, _projectPath);
                return [];
            }

            using var json = await JsonDocument.ParseAsync(
                await response.Content.ReadAsStreamAsync(cancellationToken),
                cancellationToken: cancellationToken);

            if (json.RootElement.ValueKind != JsonValueKind.Array) return [];

            var tickets = new List<Ticket>(json.RootElement.GetArrayLength());
            foreach (var issue in json.RootElement.EnumerateArray())
            {
                var iid = issue.TryGetProperty("iid", out var iidEl)
                    ? iidEl.GetInt64().ToString()
                    : null;
                if (iid is null) continue;

                var title = issue.TryGetProperty("title", out var t) ? t.GetString() ?? "" : "";
                var description = issue.TryGetProperty("description", out var d) && d.ValueKind != JsonValueKind.Null
                    ? d.GetString() ?? ""
                    : "";
                var state = issue.TryGetProperty("state", out var s) ? s.GetString() ?? "" : "";
                var labels = issue.TryGetProperty("labels", out var lbl) && lbl.ValueKind == JsonValueKind.Array
                    ? lbl.EnumerateArray()
                        .Where(e => e.ValueKind == JsonValueKind.String)
                        .Select(e => e.GetString() ?? string.Empty)
                        .Where(s => !string.IsNullOrEmpty(s))
                        .ToList()
                    : new List<string>();
                tickets.Add(new Ticket(new TicketId(iid), title, description, null, state, "GitLab", labels));
            }
            _logger.LogInformation("GitLab ListByLifecycleStatus: returned {Count} ticket(s)", tickets.Count);
            return tickets;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "GitLab ListByLifecycleStatus failed for project={Project} status={Status}",
                _projectPath, status);
            return [];
        }
    }

    public async Task<IReadOnlyList<Ticket>> ListByLabelsInOpenStatesAsync(
        IReadOnlyCollection<string> labels, CancellationToken cancellationToken)
    {
        if (labels.Count == 0) return [];
        _logger.LogInformation(
            "GitLab ListByLabelsInOpenStates: project={Project} labels=[{Labels}]",
            _projectPath, string.Join(", ", labels));
        try
        {
            // GitLab's labels= filter ANDs (issue must have ALL listed labels). For
            // OR-semantics across trigger labels we issue one request per label and
            // dedupe by iid in-memory.
            var deduped = new Dictionary<string, Ticket>();
            foreach (var rawLabel in labels)
            {
                var label = Uri.EscapeDataString(rawLabel);
                var url = $"{_baseUrl}/api/v4/projects/{_projectPath}/issues?labels={label}&state=opened&per_page=100";

                using var request = new HttpRequestMessage(HttpMethod.Get, url);
                request.Headers.Add("PRIVATE-TOKEN", _privateToken);

                using var response = await _httpClient.SendAsync(request, cancellationToken);
                if (!response.IsSuccessStatusCode) continue;

                using var json = await JsonDocument.ParseAsync(
                    await response.Content.ReadAsStreamAsync(cancellationToken),
                    cancellationToken: cancellationToken);
                if (json.RootElement.ValueKind != JsonValueKind.Array) continue;

                foreach (var issue in json.RootElement.EnumerateArray())
                {
                    var iid = issue.TryGetProperty("iid", out var iidEl)
                        ? iidEl.GetInt64().ToString() : null;
                    if (iid is null) continue;

                    var title = issue.TryGetProperty("title", out var t) ? t.GetString() ?? "" : "";
                    var description = issue.TryGetProperty("description", out var d)
                        && d.ValueKind != JsonValueKind.Null ? d.GetString() ?? "" : "";
                    var state = issue.TryGetProperty("state", out var s) ? s.GetString() ?? "" : "";
                    var issueLabels = issue.TryGetProperty("labels", out var lbl)
                        && lbl.ValueKind == JsonValueKind.Array
                            ? lbl.EnumerateArray()
                                .Where(e => e.ValueKind == JsonValueKind.String)
                                .Select(e => e.GetString() ?? string.Empty)
                                .Where(s => !string.IsNullOrEmpty(s))
                                .ToList()
                            : new List<string>();

                    deduped[iid] = new Ticket(
                        new TicketId(iid), title, description, null, state, "GitLab", issueLabels);
                }
            }
            _logger.LogInformation(
                "GitLab ListByLabelsInOpenStates: returned {Count} ticket(s)", deduped.Count);
            return [.. deduped.Values];
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "GitLab ListByLabelsInOpenStates failed for project={Project} labels=[{Labels}]",
                _projectPath, string.Join(", ", labels));
            return [];
        }
    }

    public async Task<IReadOnlyList<AttachmentRef>> GetAttachmentRefsAsync(
        TicketId ticketId, CancellationToken cancellationToken)
    {
        try
        {
            var ticket = await GetTicketAsync(ticketId, cancellationToken);
            var loader = new GitLabAttachmentLoader(
                _baseUrl, _projectPath, _privateToken, _httpClient,
                Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance);
            return loader.ParseRefs(ticket.Description);
        }
        catch
        {
            return [];
        }
    }

    public async Task<IReadOnlyList<TicketImageAttachment>> DownloadImageAttachmentsAsync(
        TicketId ticketId, CancellationToken cancellationToken)
    {
        var refs = await GetAttachmentRefsAsync(ticketId, cancellationToken);
        if (refs.Count == 0) return [];

        var results = new List<TicketImageAttachment>();
        foreach (var r in refs)
        {
            var content = await _attachmentLoader.DownloadAsync(r, cancellationToken);
            if (content is not null)
                results.Add(new TicketImageAttachment(r, content));
        }
        return results;
    }

    public async Task UpdateStatusAsync(
        TicketId ticketId, string comment, CancellationToken cancellationToken)
    {
        var url = $"{_baseUrl}/api/v4/projects/{_projectPath}/issues/{ticketId.Value}/notes";

        using var request = new HttpRequestMessage(HttpMethod.Post, url);
        request.Headers.Add("PRIVATE-TOKEN", _privateToken);
        request.Content = JsonContent.Create(new { body = comment });

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        await response.EnsureSuccessWithBodyAsync(cancellationToken);
    }

    public async Task CloseTicketAsync(
        TicketId ticketId, string resolution, CancellationToken cancellationToken)
    {
        // Post the resolution as a note first.
        await UpdateStatusAsync(ticketId, resolution, cancellationToken);

        // Then close the issue.
        var url = $"{_baseUrl}/api/v4/projects/{_projectPath}/issues/{ticketId.Value}";

        using var request = new HttpRequestMessage(HttpMethod.Put, url);
        request.Headers.Add("PRIVATE-TOKEN", _privateToken);
        request.Content = JsonContent.Create(new { state_event = "close" });

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        await response.EnsureSuccessWithBodyAsync(cancellationToken);
    }

    public async Task TransitionToAsync(
        TicketId ticketId, string statusName, CancellationToken cancellationToken)
    {
        var stateEvent = statusName.ToLowerInvariant() switch
        {
            "closed" => "close",
            "opened" or "open" or "reopen" => "reopen",
            _ => statusName.ToLowerInvariant()
        };

        var url = $"{_baseUrl}/api/v4/projects/{_projectPath}/issues/{ticketId.Value}";

        using var request = new HttpRequestMessage(HttpMethod.Put, url);
        request.Headers.Add("PRIVATE-TOKEN", _privateToken);
        request.Content = JsonContent.Create(new { state_event = stateEvent });

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        await response.EnsureSuccessWithBodyAsync(cancellationToken);
    }
}
