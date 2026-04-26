using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Exceptions;
using AgentSmith.Domain.Models;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Infrastructure.Services.Providers.Tickets;

/// <summary>
/// Fetches issues from Jira Cloud using REST API v3.
/// </summary>
public sealed class JiraTicketProvider : ITicketProvider
{
    private readonly string _baseUrl;
    private readonly HttpClient _httpClient;
    private readonly ILogger<JiraTicketProvider> _logger;
    private readonly IAttachmentLoader _attachmentLoader;
    private readonly string _doneStatus;
    private readonly string _closeTransitionName;
    private readonly string? _projectKey;

    public string ProviderType => "Jira";

    public JiraTicketProvider(
        string baseUrl,
        string email,
        string apiToken,
        HttpClient httpClient,
        ILogger<JiraTicketProvider> logger,
        string? doneStatus = null,
        string? closeTransitionName = null,
        string? projectKey = null)
    {
        _baseUrl = baseUrl.TrimEnd('/');
        _httpClient = httpClient;
        _logger = logger;
        _doneStatus = doneStatus ?? "Done";
        _closeTransitionName = closeTransitionName ?? "Close";
        _projectKey = projectKey;

        var credentials = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{email}:{apiToken}"));
        _httpClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Basic", credentials);
        _httpClient.DefaultRequestHeaders.Accept.Add(
            new MediaTypeWithQualityHeaderValue("application/json"));

        _attachmentLoader = new JiraAttachmentLoader(_httpClient, _logger);
    }

    public async Task<Ticket> GetTicketAsync(
        TicketId ticketId, CancellationToken cancellationToken)
    {
        var url = $"{_baseUrl}/rest/api/3/issue/{ticketId.Value}?fields=summary,description,status,attachment";
        var response = await _httpClient.GetAsync(url, cancellationToken);

        if (response.StatusCode == HttpStatusCode.NotFound)
            throw new TicketNotFoundException(ticketId);

        await response.EnsureSuccessWithBodyAsync(cancellationToken);

        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        var fields = root.GetProperty("fields");

        var title = fields.TryGetProperty("summary", out var summaryEl)
            ? summaryEl.GetString() ?? ""
            : "";

        var description = fields.TryGetProperty("description", out var descEl)
            && descEl.ValueKind != JsonValueKind.Null
            ? JiraAdfParser.ExtractText(descEl)
            : "";

        var status = fields.TryGetProperty("status", out var statusEl)
            && statusEl.TryGetProperty("name", out var statusNameEl)
            ? statusNameEl.GetString() ?? ""
            : "";

        return new Ticket(
            ticketId,
            title,
            description,
            acceptanceCriteria: null,
            status,
            "Jira");
    }

    public async Task<IReadOnlyList<Ticket>> ListByLifecycleStatusAsync(
        TicketLifecycleStatus status, CancellationToken cancellationToken)
    {
        try
        {
            var label = LifecycleLabels.For(status);
            var jql = _projectKey is null
                ? $"labels = \"{label}\""
                : $"project = \"{_projectKey}\" AND labels = \"{label}\"";

            var url = $"{_baseUrl}/rest/api/3/search";
            var body = JsonSerializer.Serialize(new
            {
                jql,
                fields = new[] { "summary", "description", "status" },
                maxResults = 100
            });

            var content = new StringContent(body, Encoding.UTF8, "application/json");
            var response = await _httpClient.PostAsync(url, content, cancellationToken);
            if (!response.IsSuccessStatusCode) return [];

            using var doc = JsonDocument.Parse(
                await response.Content.ReadAsStringAsync(cancellationToken));

            if (!doc.RootElement.TryGetProperty("issues", out var issuesEl)
                || issuesEl.ValueKind != JsonValueKind.Array)
                return [];

            var tickets = new List<Ticket>(issuesEl.GetArrayLength());
            foreach (var issue in issuesEl.EnumerateArray())
            {
                var key = issue.TryGetProperty("key", out var keyEl) ? keyEl.GetString() : null;
                if (key is null) continue;

                var fields = issue.TryGetProperty("fields", out var f) ? f : default;
                var title = fields.ValueKind != JsonValueKind.Undefined
                    && fields.TryGetProperty("summary", out var sEl)
                        ? sEl.GetString() ?? ""
                        : "";
                var description = fields.ValueKind != JsonValueKind.Undefined
                    && fields.TryGetProperty("description", out var dEl)
                    && dEl.ValueKind != JsonValueKind.Null
                        ? JiraAdfParser.ExtractText(dEl)
                        : "";
                var statusName = fields.ValueKind != JsonValueKind.Undefined
                    && fields.TryGetProperty("status", out var stEl)
                    && stEl.TryGetProperty("name", out var stNameEl)
                        ? stNameEl.GetString() ?? ""
                        : "";

                tickets.Add(new Ticket(new TicketId(key), title, description, null, statusName, "Jira"));
            }
            return tickets;
        }
        catch
        {
            return [];
        }
    }

    public async Task<IReadOnlyList<AttachmentRef>> GetAttachmentRefsAsync(
        TicketId ticketId, CancellationToken cancellationToken)
    {
        var url = $"{_baseUrl}/rest/api/3/issue/{ticketId.Value}?fields=attachment";
        var response = await _httpClient.GetAsync(url, cancellationToken);

        if (!response.IsSuccessStatusCode)
            return [];

        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        using var doc = JsonDocument.Parse(json);

        if (!doc.RootElement.TryGetProperty("fields", out var fields))
            return [];

        return JiraAttachmentLoader.ParseRefs(fields);
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
        var url = $"{_baseUrl}/rest/api/3/issue/{ticketId.Value}/comment";

        var body = JsonSerializer.Serialize(new
        {
            body = new
            {
                type = "doc",
                version = 1,
                content = new[]
                {
                    new
                    {
                        type = "paragraph",
                        content = new[]
                        {
                            new
                            {
                                type = "text",
                                text = comment
                            }
                        }
                    }
                }
            }
        });

        var content = new StringContent(body, Encoding.UTF8, "application/json");
        var response = await _httpClient.PostAsync(url, content, cancellationToken);
        await response.EnsureSuccessWithBodyAsync(cancellationToken);
    }

    public async Task CloseTicketAsync(
        TicketId ticketId, string resolution, CancellationToken cancellationToken)
    {
        await UpdateStatusAsync(ticketId, resolution, cancellationToken);
        await TransitionToAsync(ticketId, _doneStatus, _closeTransitionName, cancellationToken);
    }

    public async Task TransitionToAsync(
        TicketId ticketId, string statusName, CancellationToken cancellationToken)
    {
        await TransitionToAsync(ticketId, statusName, null, cancellationToken);
    }

    private async Task TransitionToAsync(
        TicketId ticketId, string primaryName, string? fallbackName,
        CancellationToken cancellationToken)
    {
        var transitionsUrl = $"{_baseUrl}/rest/api/3/issue/{ticketId.Value}/transitions";
        var transitionsResponse = await _httpClient.GetAsync(transitionsUrl, cancellationToken);
        await transitionsResponse.EnsureSuccessWithBodyAsync(cancellationToken);

        var transitionsJson = await transitionsResponse.Content.ReadAsStringAsync(cancellationToken);
        using var transitionsDoc = JsonDocument.Parse(transitionsJson);

        var transitionId = FindTransitionId(transitionsDoc.RootElement, primaryName, fallbackName);
        if (transitionId is null)
        {
            _logger.LogWarning(
                "No transition matching '{StatusName}' found for ticket {TicketId}. The ticket will remain in its current state.",
                primaryName, ticketId.Value);
            return;
        }

        var transitionBody = JsonSerializer.Serialize(new
        {
            transition = new { id = transitionId }
        });

        var transitionContent = new StringContent(transitionBody, Encoding.UTF8, "application/json");
        var transitionResponse = await _httpClient.PostAsync(transitionsUrl, transitionContent, cancellationToken);
        await transitionResponse.EnsureSuccessWithBodyAsync(cancellationToken);
    }

    private static string? FindTransitionId(
        JsonElement root, string primaryName, string? fallbackName)
    {
        if (!root.TryGetProperty("transitions", out var transitions))
            return null;

        foreach (var transition in transitions.EnumerateArray())
        {
            var name = transition.TryGetProperty("name", out var nameEl)
                ? nameEl.GetString()
                : null;

            if (name is null) continue;

            if (name.Contains(primaryName, StringComparison.OrdinalIgnoreCase)
                || (fallbackName is not null && name.Contains(fallbackName, StringComparison.OrdinalIgnoreCase)))
            {
                return transition.TryGetProperty("id", out var idEl)
                    ? idEl.GetString()
                    : null;
            }
        }

        return null;
    }

}
