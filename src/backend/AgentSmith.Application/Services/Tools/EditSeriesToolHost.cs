using System.ComponentModel;
using AgentSmith.Application.Models;
using AgentSmith.Contracts.Dialogue;
using Microsoft.Extensions.AI;

namespace AgentSmith.Application.Services.Tools;

/// <summary>
/// edit_series — changes the bound ticket's series for its next attempt on the person's word, without a proposal
/// card. Offered only to the master of a bound turn on a worked ticket whose poster may start runs; never to a
/// sub-agent. Recording starts nothing.
/// </summary>
public sealed class EditSeriesToolHost(ISeriesEdit edit) : IToolHost
{
    public IEnumerable<AIFunction> GetTools(SkillExecutionPhase? stage, string? investigatorMode)
    {
        _ = stage;
        _ = investigatorMode;
        return [AIFunctionFactory.Create(EditSeries, name: "edit_series")];
    }

    [Description(
        "Change this ticket's series for its next attempt because the person asked for it. Each edit is "
        + "{kind, specId, yaml}: 'recut' replaces a spec's text (yaml is the full spec document and its spec field "
        + "must equal specId), 'drop' removes a spec that has not run (specId only), 'append' adds a spec at the end "
        + "(yaml only; its id is minted anew). Every yaml must be schema-valid. A spec that already ran stays as it "
        + "ran. The answer lists every spec's disposition with its final id, or the reason nothing was recorded. "
        + "It starts nothing: tell the person what changed, verify it, and start only when they say go.")]
    private async Task<string> EditSeries(
        [Description("The edits, applied in order to the series read_series shows.")] SeriesEdit[] edits,
        CancellationToken cancellationToken) =>
        await edit.EditAsync(edits ?? [], cancellationToken);
}
