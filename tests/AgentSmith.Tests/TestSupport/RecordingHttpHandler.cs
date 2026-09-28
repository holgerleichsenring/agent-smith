namespace AgentSmith.Tests.TestSupport;

/// <summary>
/// Records every request a chat API client sends and answers with one body that satisfies
/// Slack (ok), the Bot Framework token endpoint (access_token) and a Teams activity post (id).
/// </summary>
internal sealed class RecordingHttpHandler : HttpMessageHandler
{
    public List<(string Url, string Body)> Requests { get; } = [];

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var body = request.Content is null ? string.Empty : await request.Content.ReadAsStringAsync(cancellationToken);
        lock (Requests) Requests.Add((request.RequestUri!.ToString(), body));
        return new HttpResponseMessage(System.Net.HttpStatusCode.OK)
        {
            Content = new StringContent("{\"ok\":true,\"access_token\":\"token\",\"expires_in\":3600,\"id\":\"act-1\"}"),
        };
    }
}
