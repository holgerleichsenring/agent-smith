using AgentSmith.Application.Models;
using AgentSmith.Application.Services.Handlers;
using AgentSmith.Contracts.Activation;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentSmith.Tests.Handlers;

public sealed class PipelineNameInitializerHandlerTests
{
    private readonly PipelineNameInitializerHandler _sut = new(
        RunStateConceptsTestFactory.Default,
        NullLogger<PipelineNameInitializerHandler>.Instance);

    [Fact]
    public async Task ExecuteAsync_RetiredPipelineName_ThrowsRatherThanPublishingIt()
    {
        // p0393 canonicalised a retired name to `code` here, because the run had genuinely
        // been started under the alias. 2026-09-25-e5b1 deleted the alias map, so no run can
        // reach this handler under one — and if one somehow did, the vocabulary refuses the
        // word rather than letting every concept-keyed rule key off a name nothing declares.
        var context = new PipelineNameInitializerContext(PipelineFor("fix-bug"));

        var act = async () => await _sut.ExecuteAsync(context, CancellationToken.None);

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task ExecuteAsync_ApiSecurityScanPipeline_PublishesPipelineNameApiSecurityScan()
    {
        var pipeline = PipelineFor("api-security-scan");
        var context = new PipelineNameInitializerContext(pipeline);

        await _sut.ExecuteAsync(context, CancellationToken.None);

        var concepts = RunStateConceptsTestFactory.Default(pipeline);
        concepts.GetEnum("pipeline_name").Should().Be("api-security-scan");
    }

    [Fact]
    public async Task ExecuteAsync_PipelineNotInEnum_ThrowsAtSetEnum()
    {
        // Defensive: typo / unknown preset still fails loud at the concept
        // writer instead of silently mis-routing downstream triage.
        var pipeline = PipelineFor("not-a-real-pipeline");
        var context = new PipelineNameInitializerContext(pipeline);

        var act = async () => await _sut.ExecuteAsync(context, CancellationToken.None);

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task ExecuteAsync_RunBeforeAnyOtherHandler_PipelineNameReadableInDownstream()
    {
        var pipeline = PipelineFor("security-scan");
        await _sut.ExecuteAsync(new PipelineNameInitializerContext(pipeline), CancellationToken.None);

        // Downstream readers see the published value via a freshly-created run-state view.
        var downstreamConcepts = RunStateConceptsTestFactory.Default(pipeline);
        downstreamConcepts.GetEnum("pipeline_name").Should().Be("security-scan");
    }

    private static PipelineContext PipelineFor(string pipelineName)
    {
        var pipeline = new PipelineContext();
        pipeline.Set(ContextKeys.ResolvedPipeline, new ResolvedPipelineConfig(
            pipelineName, new AgentConfig(), "skills", null));
        return pipeline;
    }
}
