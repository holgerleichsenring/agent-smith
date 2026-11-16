using System.Text.Json;
using AgentSmith.Domain.Models;

namespace AgentSmith.Infrastructure.Persistence.Services;

/// <summary>
/// 2026-11-16-f3fba: the one shape <see cref="EndDetail"/> takes in Run.EndDetailJson — written by the
/// run's finalization, read by the ticket's attempt view. Unreadable text reads as no detail.
/// </summary>
public static class EndDetailJson
{
    public static string? Write(EndDetail? detail) =>
        detail is null ? null : JsonSerializer.Serialize(detail, JsonSerializerOptions.Web);

    public static EndDetail? Read(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        try
        {
            return JsonSerializer.Deserialize<EndDetail>(json, JsonSerializerOptions.Web);
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
