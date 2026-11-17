using System.Text.Json;
using AgentSmith.Contracts.Reviews;
using AgentSmith.Infrastructure.Persistence.Entities;

namespace AgentSmith.Infrastructure.Persistence.Repositories;

/// <summary>2026-11-17-3435a: a record's CI columns to and from their stored shape; the failures are a JSON array.</summary>
public static class PrRecordCiJson
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);

    public static string? Failures(IReadOnlyList<CiFailure> failures) =>
        failures.Count == 0 ? null : JsonSerializer.Serialize(failures, Options);

    public static PrRecordCiView? View(PrReviewRecord record) => record.CiState is not { } state ? null
        : new PrRecordCiView(state, record.CiHeadSha,
            record.CiAtTicks is { } t ? new DateTimeOffset(t, TimeSpan.Zero) : null, Read(record.CiFailuresJson));

    private static IReadOnlyList<CiFailure> Read(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return [];
        try { return JsonSerializer.Deserialize<List<CiFailure>>(json, Options) ?? []; }
        catch (JsonException) { return []; }
    }
}
