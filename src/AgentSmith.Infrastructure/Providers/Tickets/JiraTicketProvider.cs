using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using AgentSmith.Contracts.Providers;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Exceptions;
using AgentSmith.Domain.ValueObjects;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Infrastructure.Providers.Tickets;

/// <summary>
/// Fetches issues from Jira Cloud using REST API v3.
/// </summary>
public sealed class JiraTicketProvider : ITicketProvider
{
    private readonly string _baseUrl;
    private readonly HttpClient _httpClient;
    private readonly ILogger<JiraTicketProvider> _logger;

    public string ProviderType => "Jira";

    public JiraTicketProvider(
        string baseUrl,
        string email,
        string apiToken,
        HttpClient httpClient,
        ILogger<JiraTicketProvider> logger)
    {
        _baseUrl = baseUrl.TrimEnd('/');
        _httpClient = httpClient;
        _logger = logger;

        var credentials = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{email}:{apiToken}"));
        _httpClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Basic", credentials);
        _httpClient.DefaultRequestHeaders.Accept.Add(
            new MediaTypeWithQualityHeaderValue("application/json"));
    }

    public async Task<Ticket> GetTicketAsync(
        TicketId ticketId, CancellationToken cancellationToken = default)
    {
        var url = $"{_baseUrl}/rest/api/3/issue/{ticketId.Value}?fields=summary,description,status";
        var response = await _httpClient.GetAsync(url, cancellationToken);

        if (response.StatusCode == HttpStatusCode.NotFound)
            throw new TicketNotFoundException(ticketId);

        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        var fields = root.GetProperty("fields");

        var title = fields.TryGetProperty("summary", out var summaryEl)
            ? summaryEl.GetString() ?? ""
            : "";

        var description = fields.TryGetProperty("description", out var descEl)
            && descEl.ValueKind != JsonValueKind.Null
            ? ExtractTextFromAdf(descEl)
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

    public async Task UpdateStatusAsync(
        TicketId ticketId, string comment, CancellationToken cancellationToken = default)
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
        response.EnsureSuccessStatusCode();
    }

    public async Task CloseTicketAsync(
        TicketId ticketId, string resolution, CancellationToken cancellationToken = default)
    {
        // Post the resolution as a comment first.
        await UpdateStatusAsync(ticketId, resolution, cancellationToken);

        // Fetch available transitions.
        var transitionsUrl = $"{_baseUrl}/rest/api/3/issue/{ticketId.Value}/transitions";
        var transitionsResponse = await _httpClient.GetAsync(transitionsUrl, cancellationToken);
        transitionsResponse.EnsureSuccessStatusCode();

        var transitionsJson = await transitionsResponse.Content.ReadAsStringAsync(cancellationToken);
        using var transitionsDoc = JsonDocument.Parse(transitionsJson);

        var transitionId = FindCloseTransitionId(transitionsDoc.RootElement);
        if (transitionId is null)
        {
            _logger.LogWarning(
                "No closing transition found for ticket {TicketId}. The ticket will remain in its current state.",
                ticketId.Value);
            return;
        }

        // Execute the transition.
        var transitionBody = JsonSerializer.Serialize(new
        {
            transition = new { id = transitionId }
        });

        var transitionContent = new StringContent(transitionBody, Encoding.UTF8, "application/json");
        var transitionResponse = await _httpClient.PostAsync(transitionsUrl, transitionContent, cancellationToken);
        transitionResponse.EnsureSuccessStatusCode();
    }

    /// <summary>
    /// Finds a transition whose name contains "Done" or "Close" (case-insensitive).
    /// </summary>
    private static string? FindCloseTransitionId(JsonElement root)
    {
        if (!root.TryGetProperty("transitions", out var transitions))
            return null;

        foreach (var transition in transitions.EnumerateArray())
        {
            var name = transition.TryGetProperty("name", out var nameEl)
                ? nameEl.GetString()
                : null;

            if (name is null)
                continue;

            if (name.Contains("Done", StringComparison.OrdinalIgnoreCase)
                || name.Contains("Close", StringComparison.OrdinalIgnoreCase))
            {
                return transition.TryGetProperty("id", out var idEl)
                    ? idEl.GetString()
                    : null;
            }
        }

        return null;
    }

    /// <summary>
    /// Recursively walks an Atlassian Document Format (ADF) tree and extracts all text node values.
    /// </summary>
    private static string ExtractTextFromAdf(JsonElement element)
    {
        var builder = new StringBuilder();
        CollectTextNodes(element, builder);
        return builder.ToString().Trim();
    }

    private static void CollectTextNodes(JsonElement element, StringBuilder builder)
    {
        if (element.ValueKind != JsonValueKind.Object)
            return;

        // If this node is a text node, grab its text value.
        if (element.TryGetProperty("type", out var typeEl)
            && typeEl.GetString() == "text"
            && element.TryGetProperty("text", out var textEl))
        {
            builder.Append(textEl.GetString());
        }

        // Recurse into the content array if present.
        if (element.TryGetProperty("content", out var contentEl)
            && contentEl.ValueKind == JsonValueKind.Array)
        {
            foreach (var child in contentEl.EnumerateArray())
            {
                CollectTextNodes(child, builder);
            }

            // Add a newline after block-level nodes (paragraphs, headings, etc.)
            // but only if we actually collected text from children.
            if (typeEl.ValueKind == JsonValueKind.String)
            {
                var nodeType = typeEl.GetString();
                if (nodeType is "paragraph" or "heading" or "blockquote"
                    or "codeBlock" or "bulletList" or "orderedList" or "listItem")
                {
                    builder.AppendLine();
                }
            }
        }
    }
}
