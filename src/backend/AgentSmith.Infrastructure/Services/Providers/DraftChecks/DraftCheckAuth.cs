using System.Net.Http.Headers;
using System.Text;

namespace AgentSmith.Infrastructure.Services.Providers.DraftChecks;

/// <summary>
/// 2026-10-02-5f89b: how a draft check's request carries the token, per host kind. It sets one
/// header on the request and is never logged or written anywhere else.
/// </summary>
public sealed class DraftCheckAuth
{
    private readonly Action<HttpRequestMessage> _apply;

    private DraftCheckAuth(Action<HttpRequestMessage> apply) => _apply = apply;

    public static DraftCheckAuth GitLab(string token) =>
        new(r => r.Headers.Add("PRIVATE-TOKEN", token));

    public static DraftCheckAuth GitHub(string token) => new(r =>
    {
        r.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        r.Headers.UserAgent.ParseAdd("agent-smith");
        r.Headers.Accept.ParseAdd("application/vnd.github+json");
    });

    /// <summary>Azure DevOps (empty user) and Jira (email) sign in with Basic auth.</summary>
    public static DraftCheckAuth Basic(string user, string token) => new(r =>
        r.Headers.Authorization = new AuthenticationHeaderValue(
            "Basic", Convert.ToBase64String(Encoding.UTF8.GetBytes($"{user}:{token}"))));

    public void Apply(HttpRequestMessage request) => _apply(request);
}
