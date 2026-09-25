using AgentSmith.Contracts.Models.Skills;
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
    public async Task ExecuteAsync_ARetiredName_IsPublishedVerbatimAndNotCanonicalised()
    {
        // p0393 canonicalised a retired name to `code` HERE, because a run had genuinely been
        // started under the alias. 2026-09-25-e5b1 deleted the alias map, so the resolved name
        // IS the published one and nothing rewrites it on the way through.
        //
        // The vocabulary is pinned by the test rather than taken from the shipped catalog: the
        // catalog lives in another repository, still declares the retired names, and is reached
        // only when a skills checkout happens to be present — so a test that asserted the
        // catalog's opinion passed on a machine without one and failed on CI with one. What this
        // handler owes is that it publishes what it was given.
        var declaring = DeclaringPipelineName("fix-bug");
        var handler = new PipelineNameInitializerHandler(
            RunStateConceptsTestFactory.WithVocabulary(declaring),
            NullLogger<PipelineNameInitializerHandler>.Instance);
        var pipeline = PipelineFor("fix-bug");

        await handler.ExecuteAsync(new PipelineNameInitializerContext(pipeline), CancellationToken.None);

        RunStateConceptsTestFactory.WithVocabulary(declaring)(pipeline)
            .GetEnum("pipeline_name").Should().Be("fix-bug");
    }

    [Fact]
    public async Task ExecuteAsync_ANameTheVocabularyDoesNotDeclare_ThrowsRatherThanPublishingIt()
    {
        // The retired names are no longer presets, so a vocabulary derived from the presets does
        // not declare them — and the write fails loudly instead of letting every concept-keyed
        // rule downstream key off a word nothing declares.
        var handler = new PipelineNameInitializerHandler(
            RunStateConceptsTestFactory.WithVocabulary(RunStateConceptsTestFactory.FallbackMinimal),
            NullLogger<PipelineNameInitializerHandler>.Instance);
        var context = new PipelineNameInitializerContext(PipelineFor("fix-bug"));

        var act = async () => await handler.ExecuteAsync(context, CancellationToken.None);

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

    /// <summary>A vocabulary that declares exactly these pipeline names, and nothing else's
    /// opinion — the shipped catalog lives in another repository and is reachable only
    /// sometimes, which is not a property a handler test may depend on.</summary>
    private static ConceptVocabulary DeclaringPipelineName(params string[] names) =>
        new(new Dictionary<string, ProjectConcept>
        {
            ["pipeline_name"] = new("pipeline_name", "test", ConceptType.Enum, [.. names], null, []),
        });

    private static PipelineContext PipelineFor(string pipelineName)
    {
        var pipeline = new PipelineContext();
        pipeline.Set(ContextKeys.ResolvedPipeline, new ResolvedPipelineConfig(
            pipelineName, new AgentConfig(), "skills", null));
        return pipeline;
    }
}
