using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;

namespace AgentSmith.Application.Services.Design;

/// <summary>
/// 2026-10-01-7f7ad: the figma sources a master can read through — a run's off its
/// <see cref="ResolvedProject"/>, a design turn's off the seed its runner set. One answer for the
/// tool factory that builds design_read and the prompt that says whether a link can be read, so
/// the two never disagree. A pure function over the pipeline, hence static.
/// </summary>
public static class PipelineDesignSources
{
    public static IReadOnlyList<DesignSource> Figma(PipelineContext pipeline)
    {
        ArgumentNullException.ThrowIfNull(pipeline);
        var sources = pipeline.TryGet<ResolvedProject>(ContextKeys.ProjectConfig, out var project) && project is not null
            ? project.DesignSources
            : pipeline.TryGet<IReadOnlyList<DesignSource>>(ContextKeys.SpecDialogDesignSources, out var seeded)
              && seeded is not null ? seeded : [];
        return sources.Where(s => s.Vendor == DesignSourceVendor.Figma).ToList();
    }
}
