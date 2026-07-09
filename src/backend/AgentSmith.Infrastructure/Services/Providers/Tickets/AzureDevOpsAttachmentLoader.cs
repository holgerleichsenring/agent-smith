using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Entities;
using Microsoft.Extensions.Logging;
using Microsoft.TeamFoundation.WorkItemTracking.WebApi;
using Microsoft.VisualStudio.Services.Common;
using Microsoft.VisualStudio.Services.WebApi;

namespace AgentSmith.Infrastructure.Services.Providers.Tickets;

/// <summary>
/// Loads image attachments from Azure DevOps work items via relation expansion.
/// Filters for AttachedFile relations with image MIME types, downloads with PAT.
/// </summary>
public sealed class AzureDevOpsAttachmentLoader(
    AzureDevOpsTicketConnection connection,
    HttpClient httpClient,
    ILogger logger) : IAttachmentLoader
{
    private readonly string _organizationUrl = connection.OrganizationUrl;
    private readonly string _project = connection.Project;
    private readonly string _personalAccessToken = connection.PersonalAccessToken;

    /// <summary>
    /// Fetches the work item with expanded relations and returns ALL AttachedFile
    /// refs (p0317: documents and other binaries included — the image/document
    /// gates live in the download orchestration).
    /// </summary>
    public async Task<IReadOnlyList<AttachmentRef>> GetRefsAsync(
        int workItemId, CancellationToken cancellationToken)
    {
        var client = CreateClient();
        var workItem = await client.GetWorkItemAsync(
            _project, workItemId, expand: Microsoft.TeamFoundation.WorkItemTracking.WebApi.Models.WorkItemExpand.Relations,
            cancellationToken: cancellationToken);

        if (workItem?.Relations is null)
            return [];

        var refs = new List<AttachmentRef>();
        foreach (var relation in workItem.Relations)
        {
            if (relation.Rel != "AttachedFile") continue;

            var url = relation.Url;
            var attributes = relation.Attributes;

            var fileName = attributes?.TryGetValue("name", out var nameObj) == true
                ? nameObj?.ToString() ?? "" : Path.GetFileName(new Uri(url).AbsolutePath);

            var size = attributes?.TryGetValue("resourceSize", out var sizeObj) == true
                && long.TryParse(sizeObj?.ToString(), out var s) ? (long?)s : null;

            refs.Add(new AttachmentRef(url, fileName, AttachmentMimeTypes.Guess(fileName), size));
        }

        return refs;
    }

    public async Task<byte[]?> DownloadAsync(
        AttachmentRef attachment, CancellationToken cancellationToken)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, attachment.Uri);
            var basicCreds = Convert.ToBase64String(
                System.Text.Encoding.UTF8.GetBytes($":{_personalAccessToken}"));
            request.Headers.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", basicCreds);

            var response = await httpClient.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning("Failed to download Azure DevOps attachment {File}: {Status}",
                    attachment.FileName, response.StatusCode);
                return null;
            }

            var content = await response.Content.ReadAsByteArrayAsync(cancellationToken);
            if (content.Length > TicketImageAttachment.MaxSizeBytes)
            {
                logger.LogWarning("Azure DevOps attachment {File} exceeds 5MB limit, skipping",
                    attachment.FileName);
                return null;
            }
            return content;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Error downloading Azure DevOps attachment {File}", attachment.FileName);
            return null;
        }
    }

    private WorkItemTrackingHttpClient CreateClient()
    {
        var credentials = new VssBasicCredential(string.Empty, _personalAccessToken);
        var vssConnection = new VssConnection(new Uri(_organizationUrl), credentials);
        return vssConnection.GetClient<WorkItemTrackingHttpClient>();
    }

}
