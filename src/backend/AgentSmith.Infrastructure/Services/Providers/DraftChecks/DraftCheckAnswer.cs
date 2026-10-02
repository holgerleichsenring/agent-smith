using System.Text.Json;
using AgentSmith.Contracts.Models.ConfigStudio;

namespace AgentSmith.Infrastructure.Services.Providers.DraftChecks;

/// <summary>
/// 2026-10-02-5f89b: what one draft-check call got back, and what it means for a step. The body
/// is kept only to parse a field out of it; no step detail ever copies it.
/// </summary>
public sealed class DraftCheckAnswer
{
    private readonly string _body;

    private DraftCheckAnswer(string host, int status, string body, string? failure)
    {
        HostName = host;
        Status = status;
        _body = body;
        Failure = failure;
    }

    public string HostName { get; }
    public int Status { get; }
    /// <summary>Why no HTTP answer arrived (DNS, connect, TLS, timeout) — a sentence of ours.</summary>
    public string? Failure { get; }

    public bool IsRedirect => Status is >= 300 and < 400;
    public bool IsSuccess => Status is >= 200 and < 300;

    public static DraftCheckAnswer Answered(string host, int status, string body) => new(host, status, body, null);

    public static DraftCheckAnswer Unanswered(string host, string failure) => new(host, 0, string.Empty, failure);

    /// <summary>The body as JSON, or null when it is not JSON (a sign-in page, a proxy's error page).</summary>
    public JsonElement? Json()
    {
        try
        {
            using var document = JsonDocument.Parse(_body);
            return document.RootElement.Clone();
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>The step's failure when no usable HTTP answer arrived, else null.</summary>
    public DraftCheckStep? Unreached(string key) =>
        Failure is not null ? DraftCheckStep.Fail(key, Failure)
        : IsRedirect ? DraftCheckStep.Fail(key,
            $"{HostName} answered HTTP {Status} (a redirect); the check does not follow redirects.")
        : null;
}
