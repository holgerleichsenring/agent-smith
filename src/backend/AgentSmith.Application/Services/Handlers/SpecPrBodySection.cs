using System.Text;
using AgentSmith.Application.Services.Specs;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Specs;

namespace AgentSmith.Application.Services.Handlers;

/// <summary>
/// p0393a: the two sections a derived run adds to its pull-request body — what the
/// ticket lost, and where each phase stands.
/// <para>
/// Both exist for the same reason: the accounting and the stop are only worth
/// something if a human can see them in seconds, in the place the change is reviewed.
/// Empty when the run derived nothing, so the caller interpolates unconditionally.
/// </para>
/// </summary>
public static class SpecPrBodySection
{
    /// <param name="shortfall">p0439: the shortfall this run delivers, if any — the phase
    /// table then reads as a delivery note and the "Not delivered" section follows it.</param>
    public static string Build(
        PipelineContext pipeline, SpecSequenceProgress? progress, RunShortfall? shortfall = null)
    {
        ArgumentNullException.ThrowIfNull(pipeline);
        if (!pipeline.TryGet<SpecSet>(ContextKeys.SpecSet, out var set) || set is null)
            return ShortfallSection.Build(shortfall);

        var sb = new StringBuilder();
        sb.AppendLine();
        sb.AppendLine();
        if (progress is not null) sb.AppendLine(SpecPrBody.RenderStatus(progress, shortfall is not null));
        sb.AppendLine(SpecPrBody.RenderDiscarded(set));
        return sb.ToString() + ShortfallSection.Build(shortfall);
    }
}
