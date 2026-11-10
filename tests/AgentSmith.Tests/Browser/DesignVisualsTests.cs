using AgentSmith.Application.Services.Sandbox;
using AgentSmith.Application.Services.Tools;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Sandbox;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentSmith.Tests.Browser;

/// <summary>2026-11-10-14a3d: an upload holding a page is something visual; a source upload is not.</summary>
public sealed class DesignVisualsTests
{
    [Fact]
    public void DesignSurface_WebsiteUpload_HasRenderAndMockTools()
    {
        DesignVisuals.Present(Turn("index.html", "styles/site.css")).Should().BeTrue();
        DesignVisuals.Present(Turn("app/main.py", "spec-doc/prd.md")).Should().BeFalse();
        DesignVisuals.Present(new PipelineContext()).Should().BeFalse();
    }

    private static PipelineContext Turn(params string[] files)
    {
        var upload = new ReferenceSetSandbox(new ResolvedProject { Name = "p" }, "c-1", "reference:site", "set-1",
            null!, null!, null!, NullLogger.Instance)
        {
            Coverage = new ReferenceCoverage(new HashSet<string>(files), new HashSet<string>(), 0),
        };
        var pipeline = new PipelineContext();
        pipeline.Set(ContextKeys.Sandboxes, (IReadOnlyDictionary<string, ISandbox>)new Dictionary<string, ISandbox>
        {
            ["reference:site"] = upload,
        });
        return pipeline;
    }
}
