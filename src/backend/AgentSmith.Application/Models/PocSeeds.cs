using System.Text.Json;
using AgentSmith.Contracts.Commands;

namespace AgentSmith.Application.Models;

/// <summary>
/// 2026-11-23-933dc: what the server seeded into a poc-deploy run — strings, so the request survives the queue — read
/// once: the PoC and revision (null when missing or not a number), the actor, and for an overrule run the reason and
/// the blocking finding ids it lifts.
/// </summary>
public sealed record PocSeeds(
    long? PocId, int? Revision, string? Actor, bool Overrule, string? OverruleReason, IReadOnlyList<string> OverruleIds)
{
    /// <summary>2026-11-23-c81ed: an apply's readiness seed — null keeps the row's path.</summary>
    public string? Readiness { get; init; }

    /// <summary>2026-11-23-c81ee: why the lifetime pass started this stop or delete (PocLifetimeCases); null for an owner's act.</summary>
    public string? LifetimeCase { get; init; }

    /// <summary>2026-11-23-c81ee: the person who asked for a poc-extend holds pocs.admin.</summary>
    public bool MayExceed { get; init; }

    public static PocSeeds From(PipelineContext pipeline)
    {
        ArgumentNullException.ThrowIfNull(pipeline);
        return new PocSeeds(
            long.TryParse(Text(pipeline, ContextKeys.PocId), out var id) ? id : null,
            int.TryParse(Text(pipeline, ContextKeys.PocRevision), out var revision) ? revision : null,
            Text(pipeline, ContextKeys.PocActor),
            string.Equals(Text(pipeline, ContextKeys.PocOverrule), "true", StringComparison.OrdinalIgnoreCase),
            Text(pipeline, ContextKeys.PocOverruleReason),
            Ids(Text(pipeline, ContextKeys.PocOverruleIds)))
        {
            Readiness = Text(pipeline, ContextKeys.PocReadiness),
            LifetimeCase = Text(pipeline, ContextKeys.PocLifetimeCase),
            MayExceed = string.Equals(Text(pipeline, ContextKeys.PocMayExceed), "true", StringComparison.OrdinalIgnoreCase),
        };
    }

    private static IReadOnlyList<string> Ids(string? json) =>
        string.IsNullOrWhiteSpace(json) ? [] : JsonSerializer.Deserialize<List<string>>(json) ?? [];

    private static string? Text(PipelineContext pipeline, string key) =>
        pipeline.TryGet<object>(key, out var value) && !string.IsNullOrWhiteSpace(value?.ToString()) ? value.ToString() : null;
}
