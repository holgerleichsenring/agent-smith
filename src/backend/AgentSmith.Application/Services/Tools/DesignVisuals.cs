using AgentSmith.Application.Services.Sandbox;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Sandbox;

namespace AgentSmith.Application.Services.Tools;

/// <summary>
/// 2026-11-10-14a3d: whether a design turn has something visual for render_reference, compare_reference and the mock
/// tools to work on — an upload holding a page, a design source, a design system, or a mock already written in the
/// conversation. A turn on a source upload had all four tools and nothing to point them at. An upload whose files the
/// turn does not know yet counts as visual, so a tool is never withheld for want of a listing.
/// </summary>
public static class DesignVisuals
{
    public static bool Present(PipelineContext pipeline)
    {
        ArgumentNullException.ThrowIfNull(pipeline);
        if (pipeline.TryGet<IReadOnlyList<DesignSource>>(ContextKeys.SpecDialogDesignSources, out var sources) && sources is { Count: > 0 })
            return true;
        if (pipeline.TryGet<IReadOnlyList<ContextDocument>>(ContextKeys.DesignSystem, out var system) && system is { Count: > 0 })
            return true;
        if (pipeline.TryGet<bool>(ContextKeys.SpecDialogHasMocks, out var mocks) && mocks) return true;
        return pipeline.TryGet<IReadOnlyDictionary<string, ISandbox>>(ContextKeys.Sandboxes, out var map) && map is not null
            && map.Values.OfType<ReferenceSetSandbox>().Any(upload => upload.Coverage is not { } coverage
                || coverage.Readable.Any(path => path.EndsWith(".html", StringComparison.OrdinalIgnoreCase)
                    || path.EndsWith(".htm", StringComparison.OrdinalIgnoreCase)));
    }
}
