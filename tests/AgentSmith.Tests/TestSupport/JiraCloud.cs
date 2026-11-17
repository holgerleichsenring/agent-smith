using System.Net;
using System.Text;

namespace AgentSmith.Tests.TestSupport;

/// <summary>
/// 2026-11-17-7c9bb: an HTTP handler answering like Jira Cloud's /rest/api/3/search/jql — HTTP 400
/// "Invalid request payload" to a body carrying startAt, 150 issues in two pages joined by nextPageToken,
/// and a UTC /myself.
/// </summary>
public static class JiraCloud
{
    public static HttpMessageHandler Handler(List<string> searchBodies) => new Answering(searchBodies);

    private sealed class Answering(List<string> bodies) : HttpMessageHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            if (request.RequestUri!.AbsolutePath.EndsWith("/myself")) return Ok("""{ "timeZone": "UTC" }""");
            var body = request.Content is null ? string.Empty : await request.Content.ReadAsStringAsync(ct);
            bodies.Add(body);
            if (body.Contains("startAt"))
                return new HttpResponseMessage(HttpStatusCode.BadRequest)
                {
                    Content = new StringContent("""{ "errorMessages": ["Invalid request payload."] }""", Encoding.UTF8, "application/json"),
                };
            var second = body.Contains("\"nextPageToken\":\"page-2\"");
            var issues = string.Join(",", Enumerable.Range(second ? 100 : 0, second ? 50 : 100)
                .Select(i => $$"""{ "key": "A-{{i}}", "fields": { "updated": "2026-10-08T10:00:30.000+0000" } }"""));
            return Ok(second
                ? $$"""{ "issues": [ {{issues}} ], "isLast": true }"""
                : $$"""{ "issues": [ {{issues}} ], "nextPageToken": "page-2", "isLast": false }""");
        }

        private static HttpResponseMessage Ok(string json) =>
            new(HttpStatusCode.OK) { Content = new StringContent(json, Encoding.UTF8, "application/json") };
    }
}
