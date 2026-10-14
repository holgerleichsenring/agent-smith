using System.ComponentModel;
using AgentSmith.Application.Models;
using AgentSmith.Application.Services.SpecDialog;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Dialogue;
using AgentSmith.Contracts.Models.Configuration;
using Microsoft.Extensions.AI;

namespace AgentSmith.Application.Services.Tools;

/// <summary>
/// verify_series — reviews the specs of the bound ticket's series that have not run against the code at the ticket
/// branch. An approved edit skips the in-run cut review, so this is the check an edited series gets.
/// </summary>
public sealed class VerifySeriesToolHost(
    ISeriesRead series, SeriesVerification verification, PipelineContext pipeline, AgentConfig agent) : IToolHost
{
    public IEnumerable<AIFunction> GetTools(SkillExecutionPhase? stage, string? investigatorMode)
    {
        _ = stage;
        _ = investigatorMode;
        return [AIFunctionFactory.Create(VerifySeries, name: "verify_series")];
    }

    [Description(
        "Verify this ticket's series as its next attempt will run it against the code: every spec that has not run "
        + "is reviewed, and the cited files of those whose predecessors all ran are read at the ticket branch. Call "
        + "it after edit_series and tell the person the findings before they say go.")]
    private async Task<string> VerifySeries(CancellationToken cancellationToken) =>
        await verification.VerifyAsync(await series.ReadAsync(cancellationToken), pipeline, agent, cancellationToken);
}
