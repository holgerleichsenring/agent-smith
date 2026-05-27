using System.Text.Json;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Entities;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Infrastructure.Services.Providers.Tickets;

/// <summary>
/// Loads image attachments from Jira tickets via the REST API.
/// Parses the fields.attachment array and downloads content URLs with the
/// pre-configured Basic Auth on the shared HttpClient.
/// </summary>
public sealed class JiraAttachmentLoader(
    HttpClient httpClient,
    ILogger logger) : IAttachmentLoader
{
    /// <summary>
    /// Parses the Jira REST API response for attachment refs.
    /// Call with the raw JSON root element from /rest/api/3/issue/{key}?fields=attachment.
    /// </summary>
    public static IReadOnlyList<AttachmentRef> ParseRefs(JsonElement fieldsElement)
    {
        if (!fieldsElement.TryGetProperty("attachment", out var attachments))
            return [];

        var refs = new List<AttachmentRef>();
        foreach (var att in attachments.EnumerateArray())
        {
            var mimeType = att.TryGetProperty("mimeType", out var mime)
                ? mime.GetString() ?? "" : "";
            var fileName = att.TryGetProperty("filename", out var fn)
                ? fn.GetString() ?? "" : "";
            var contentUrl = att.TryGetProperty("content", out var url)
                ? url.GetString() ?? "" : "";
            var size = att.TryGetProperty("size", out var sz)
                ? (long?)sz.GetInt64() : null;

            if (string.IsNullOrEmpty(contentUrl)) continue;
            if (!TicketImageAttachment.IsSupportedImage(mimeType)) continue;
            if (size > TicketImageAttachment.MaxSizeBytes) continue;

            refs.Add(new AttachmentRef(contentUrl, fileName, mimeType, size));
        }

        return refs;
    }

    public async Task<byte[]?> DownloadAsync(
        AttachmentRef attachment, CancellationToken cancellationToken)
    {
        try
        {
            var response = await httpClient.GetAsync(attachment.Uri, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning("Failed to download Jira attachment {File}: {Status}",
                    attachment.FileName, response.StatusCode);
                return null;
            }

            var content = await response.Content.ReadAsByteArrayAsync(cancellationToken);
            if (content.Length > TicketImageAttachment.MaxSizeBytes)
            {
                logger.LogWarning("Jira attachment {File} exceeds 5MB limit ({Size} bytes), skipping",
                    attachment.FileName, content.Length);
                return null;
            }

            return content;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Error downloading Jira attachment {File}", attachment.FileName);
            return null;
        }
    }
}
