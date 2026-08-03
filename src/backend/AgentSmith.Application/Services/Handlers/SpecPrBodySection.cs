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
    public static string Build(PipelineContext pipeline, SpecSequenceProgress? progress)
    {
        ArgumentNullException.ThrowIfNull(pipeline);
        if (!pipeline.TryGet<SpecSet>(ContextKeys.SpecSet, out var set) || set is null)
            return string.Empty;

        var sb = new StringBuilder();
        sb.AppendLine();
        sb.AppendLine();
        if (progress is not null) sb.AppendLine(SpecPrBody.RenderStatus(progress));
        sb.AppendLine(SpecPrBody.RenderDiscarded(set));
        return sb.ToString();
    }
}
