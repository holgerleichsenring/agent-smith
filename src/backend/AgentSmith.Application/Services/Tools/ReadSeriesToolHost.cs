using System.ComponentModel;
using System.Text;
using AgentSmith.Application.Models;
using AgentSmith.Contracts.Dialogue;
using Microsoft.Extensions.AI;

namespace AgentSmith.Application.Services.Tools;

/// <summary>
/// read_series — the bound ticket's series as its next attempt will read it: every spec's id, goal, state, whether
/// it ran, and its yaml. Offered only to the master of a bound turn on a worked ticket.
/// </summary>
public sealed class ReadSeriesToolHost(ISeriesRead series) : IToolHost
{
    public IEnumerable<AIFunction> GetTools(SkillExecutionPhase? stage, string? investigatorMode)
    {
        _ = stage;
        _ = investigatorMode;
        return [AIFunctionFactory.Create(ReadSeries, name: "read_series")];
    }

    [Description(
        "Read this ticket's series as its next attempt will run it: the specs its attempts published, with a "
        + "recorded edit nobody has worked yet already applied. Per spec: its id, goal, state (done, failed, "
        + "handed_back, in_progress, not_started or carried), whether it ran — a spec that ran cannot be changed — "
        + "and its full yaml. Read it before edit_series.")]
    private async Task<string> ReadSeries(CancellationToken cancellationToken) =>
        Render(await series.ReadAsync(cancellationToken));

    /// <summary>The reading as the model is shown it.</summary>
    public static string Render(SeriesReading reading)
    {
        ArgumentNullException.ThrowIfNull(reading);
        if (reading.Failure is { } failure) return failure;
        if (reading.Specs.Count == 0)
            return reading.SeriesId is { } named // 2026-11-18-d6d2b: a record naming a series the branch did not answer
                ? $"The ticket's record names series {named}, but no spec of it was read from the ticket branch. That is a "
                  + "failed read, not an empty series: say so, and do not conclude there is nothing to run."
                : "The ticket's series holds no spec.";
        var text = new StringBuilder($"Series {reading.SeriesId ?? "(unnamed)"}, {reading.Specs.Count} spec(s):\n");
        foreach (var spec in reading.Specs)
            text.Append($"\n## {spec.Draft.SpecId} — {spec.State}, {(spec.Ran ? "ran" : "not run")}\n")
                .Append($"goal: {spec.Draft.Goal}\n```yaml\n{spec.Draft.Yaml.TrimEnd()}\n```\n");
        return text.ToString();
    }
}
