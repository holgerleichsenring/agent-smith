using AgentSmith.Contracts.Models;
using AgentSmith.Domain.Exceptions;
using AgentSmith.Domain.Models;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Infrastructure.Services.Providers.Tickets;

/// <summary>
/// Owns the Jira create: the issue payload, the ISSUE TYPE it declares, and the browse url of
/// what came back. It takes no seam of its own — the payload is already substitutable at the
/// HTTP boundary the provider's client sits on, and a second seam behind a working one would
/// be a wrapper. It moves out of the provider for the file-length ratchet's sake.
/// </summary>
internal sealed class JiraTicketCreator(
    TicketProviderHttpClient http,
    string baseUrl,
    string createEndpoint,
    string? projectKey,
    ILogger logger)
{
    /// <summary>
    /// What this create declared before an operator could choose, and what exists in every
    /// project template. Applied in the ONE clause that reads the configured kind.
    /// </summary>
    private const string DefaultIssueType = "Task";

    // The description travels as line-preserving ADF.
    public async Task<CreatedTicket> CreateAsync(
        string title, string description, IReadOnlyList<string> labels, string? kind,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(projectKey))
            throw new ConfigurationException(
                "Jira ticket creation requires a project key on the tracker connection.");
        var body = new
        {
            fields = new
            {
                project = new { key = projectKey },
                summary = title,
                issuetype = new { name = string.IsNullOrWhiteSpace(kind) ? DefaultIssueType : kind },
                description = JiraAdfRenderer.FromMultilineText(description),
                labels,
            },
        };
        using var doc = await http.SendForJsonOrThrowAsync(
            HttpMethod.Post, $"{baseUrl}{createEndpoint}", body, cancellationToken);
        var key = doc.RootElement.GetProperty("key").GetString()
            ?? throw new InvalidOperationException("Jira returned a created issue without a key.");
        logger.LogInformation("Jira created issue {Key} in project {Project}", key, projectKey);
        return new CreatedTicket(new TicketId(key), $"{baseUrl}/browse/{key}");
    }
}
