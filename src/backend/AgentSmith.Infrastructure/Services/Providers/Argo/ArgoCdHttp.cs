using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json.Nodes;
using AgentSmith.Contracts.Exceptions;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Infrastructure.Services.Providers.Argo;

/// <summary>
/// 2026-11-23-c81ec: sends one Argo CD API call with 'Authorization: Bearer &lt;token&gt;' and reads its JSON. 404 is
/// null (the caller's 'none'); 401 and 403 are refusals naming the url, path and status; an unreachable server and a 5xx
/// are transient refusals. Logs and refusals name url, path and status only — never a header, the token or a body.
/// </summary>
public sealed class ArgoCdHttp(HttpClient http, Uri server, string token, ILogger logger)
{
    public string Server => server.GetLeftPart(UriPartial.Authority);

    public async Task<JsonNode?> SendAsync(HttpMethod method, string pathAndQuery, JsonNode? body, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(method, new Uri(server, pathAndQuery));
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        if (body is not null) request.Content = new StringContent(body.ToJsonString(), Encoding.UTF8, "application/json");
        using var response = await SendAsync(request, Path(pathAndQuery), ct);
        var status = (int)response.StatusCode;
        logger.LogDebug("Argo CD {Server} answered {Status} for {Method} {Path}", Server, status, method, Path(pathAndQuery));
        if (response.StatusCode == HttpStatusCode.NotFound) return null;
        if (!response.IsSuccessStatusCode) throw Refusal(response.StatusCode, Path(pathAndQuery));
        var text = await response.Content.ReadAsStringAsync(ct);
        return string.IsNullOrWhiteSpace(text) ? new JsonObject() : Parse(text, pathAndQuery);
    }

    private async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, string path, CancellationToken ct)
    {
        try
        {
            return await http.SendAsync(request, ct);
        }
        catch (HttpRequestException ex)
        {
            logger.LogInformation(ex, "Argo CD {Server} could not be reached for {Path}", Server, path);
            throw new ArgoCdException($"Argo CD {Server} could not be reached for {path} ({ex.HttpRequestError})", isTransient: true);
        }
    }

    private JsonNode Parse(string text, string pathAndQuery)
    {
        try
        {
            return JsonNode.Parse(text) ?? new JsonObject();
        }
        catch (System.Text.Json.JsonException ex)
        {
            logger.LogWarning(ex, "Argo CD {Server} answered no JSON for {Path}", Server, Path(pathAndQuery));
            throw new ArgoCdException($"Argo CD {Server} answered no JSON for {Path(pathAndQuery)}");
        }
    }

    private ArgoCdException Refusal(HttpStatusCode status, string path)
    {
        var code = (int)status;
        var why = status is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden
            ? "; the role token is refused or its AppProject role lacks the permission (pocs.targets[].argo.token_secret)"
            : string.Empty;
        return new ArgoCdException($"Argo CD {Server} answered status {code} for {path}{why}", isTransient: code >= 500);
    }

    private static string Path(string pathAndQuery) => pathAndQuery.Split('?')[0];
}
