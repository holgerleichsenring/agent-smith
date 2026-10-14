using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;

namespace AgentSmith.Application.Services.Specs;

/// <summary>2026-10-13-8762u: what a series is read with — the <c>series:</c> block and the watchdog's wall-time
/// ceiling one attempt is sized against.</summary>
public sealed record SeriesSettings(SeriesConfig Series, int RunWallSeconds)
{
    /// <summary>Every problem of the block, the wall-clock fit included; empty when none.</summary>
    public IReadOnlyList<string> Problems() => Series.Problems(RunWallSeconds);

    /// <summary>The one sentence a reader refuses an invalid block with, or null.</summary>
    public string? Refusal() => Problems() is { Count: > 0 } problems
        ? $"The series settings are invalid: {string.Join("; ", problems)}"
        : null;

    /// <summary>The wall seconds left since the run started, or null when no ceiling applies. A run without a
    /// recorded start has the whole wall clock left.</summary>
    public int? SecondsLeft(PipelineContext pipeline, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(pipeline);
        if (RunWallSeconds <= 0) return null;
        var elapsed = pipeline.TryGet<DateTimeOffset>(ContextKeys.RunStartedAt, out var started)
            ? (now - started).TotalSeconds : 0;
        return (int)Math.Floor(RunWallSeconds - elapsed);
    }
}
