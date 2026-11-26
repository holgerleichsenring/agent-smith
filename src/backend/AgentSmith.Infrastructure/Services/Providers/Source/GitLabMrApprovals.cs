using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Infrastructure.Services.Providers.Source;

/// <summary>
/// 2026-11-26-5201f: whether a GitLab merge request's approval rules still wait for a person — GET its approvals, approvals_left
/// above 0. Read apart from detailed_merge_status, which names one blocking check at a time (a running pipeline or a draft hides
/// not_approved). No approvals_left (a tier without approval rules) or a failed read says nothing: the state read must not fail.
/// </summary>
public sealed class GitLabMrApprovals(string baseUrl, string projectPath, string token, HttpClient http, ILogger logger)
{
    public async Task<bool?> RequiredAsync(int iid, CancellationToken ct)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, $"{baseUrl}/api/v4/projects/{projectPath}/merge_requests/{iid}/approvals");
            request.Headers.Add("PRIVATE-TOKEN", token);
            using var response = await http.SendAsync(request, ct);
            await response.EnsureSuccessWithBodyAsync(ct);
            using var doc = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(ct), cancellationToken: ct);
            return Left(doc.RootElement);
        }
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            logger.LogWarning(ex, "Merge request !{Iid}: its approvals could not be read; whether it needs an approval is unknown", iid);
            return null;
        }
    }

    internal static bool? Left(JsonElement approvals) =>
        approvals.ValueKind == JsonValueKind.Object && approvals.TryGetProperty("approvals_left", out var left)
            && left.ValueKind == JsonValueKind.Number ? left.GetInt32() > 0 : null;
}
