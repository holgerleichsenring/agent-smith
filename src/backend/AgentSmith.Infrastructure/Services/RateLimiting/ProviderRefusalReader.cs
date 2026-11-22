using System.ClientModel;
using System.ClientModel.Primitives;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Infrastructure.Services.RateLimiting;

/// <summary>
/// 2026-11-22-66e4a: which provider refusals a call can outwait. Every 429 is a throughput refusal except OpenAI's
/// <c>insufficient_quota</c> — the account's credit is spent, and no wait cures it. The code is read from the JSON
/// body the refusal carries (<c>error.code</c>), not from the exception's message.
/// </summary>
internal sealed class ProviderRefusalReader(ILogger logger)
{
    private const string CreditSpent = "insufficient_quota";

    public bool IsThroughputRefusal(Exception ex) => RetryWait.IsRateLimit(ex) && !IsCreditSpent(ex);

    private bool IsCreditSpent(Exception ex)
    {
        for (Exception? e = ex; e is not null; e = e.InnerException)
        {
            if (e is ClientResultException client) return ErrorCode(client.GetRawResponse()) == CreditSpent;
        }

        return false;
    }

    private string? ErrorCode(PipelineResponse? response)
    {
        if (response?.Content is not { } content) return null;
        try
        {
            using var body = JsonDocument.Parse(content.ToMemory());
            return body.RootElement.ValueKind == JsonValueKind.Object
                && body.RootElement.TryGetProperty("error", out var error)
                && error.ValueKind == JsonValueKind.Object
                && error.TryGetProperty("code", out var code)
                && code.ValueKind == JsonValueKind.String
                    ? code.GetString()
                    : null;
        }
        catch (JsonException ex)
        {
            // An empty or non-JSON body names no code; the refusal is then read by its status alone.
            logger.LogDebug(ex, "The provider's refusal body is not JSON; no error code read");
            return null;
        }
    }
}
