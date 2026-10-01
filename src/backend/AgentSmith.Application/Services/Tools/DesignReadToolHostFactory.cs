using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;

namespace AgentSmith.Application.Services.Tools;

/// <summary>
/// 2026-10-01-7f7ab: builds design_read for a master whose project has a figma source — a run
/// reads the sources off its <see cref="ResolvedProject"/>, a design turn off the seed its
/// runner set. No source, no host: the tool is never offered where it could only fail.
/// </summary>
public sealed class DesignReadToolHostFactory(IFigmaClient figma)
{
    public DesignReadToolHost? Create(PipelineContext pipeline)
    {
        var sources = pipeline.TryGet<ResolvedProject>(ContextKeys.ProjectConfig, out var project) && project is not null
            ? project.DesignSources
            : pipeline.TryGet<IReadOnlyList<DesignSource>>(ContextKeys.SpecDialogDesignSources, out var seeded)
              && seeded is not null ? seeded : [];
        var figmaSources = sources.Where(s => s.Vendor == DesignSourceVendor.Figma).ToList();
        return figmaSources.Count == 0 ? null : new DesignReadToolHost(figma, figmaSources);
    }
}
