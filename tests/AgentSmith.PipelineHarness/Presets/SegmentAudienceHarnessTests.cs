using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Specs;
using AgentSmith.PipelineHarness.Composition;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using static AgentSmith.Tests.Specs.SampleMessagingManual;

namespace AgentSmith.PipelineHarness.Presets;

/// <summary>
/// 2026-10-17-293ba: a sample migration manual — shared setup, a publisher variant, a consumer
/// variant — cut into two specs through the production composition. The derivation states the
/// shared segments once; each spec's companion then holds those and its own variant, and never
/// the other unit's template. The first master dies on the money fence; the cut is what is read.
/// </summary>
[Trait("Category", "PipelineHarness")]
public sealed class SegmentAudienceHarnessTests
{
    [Fact]
    public async Task Harness_SharedSegments_ReachEverySpecAndAVariantOnlyItsOwn()
    {
        var tickets = new RecordingTicketProvider(body: Ticket);
        await using var harness = RealCompositionHarness.Build(FixturePaths.For(FixturePaths.Default), services =>
        {
            HarnessProjectAnalyzerStub.Register(services);
            services.RemoveAll<ITicketProviderFactory>();
            services.AddSingleton<ITicketProviderFactory>(new RecordingTicketProviderFactory(tickets));
        });
        harness.ChatClient
            .EnqueueText(SharedCut)
            .EnqueueThrow(new MasterBudgetExhaustedException("per-pipeline cost budget exhausted"));

        var runner = new PipelineRunner(harness.Services);
        await runner.RunAsync("code");

        var set = runner.LastContext!.Get<SpecSet>(ContextKeys.SpecSet);
        set.Specs.Should().HaveCount(2);
        var (publisher, consumer) = (set.Specs[0].Markdown, set.Specs[1].Markdown);
        publisher.Should().Contain(SharedTemplate).And.Contain(PublisherTemplate)
            .And.NotContain(ConsumerTemplate, "the publisher's agent must never see listener queues");
        consumer.Should().Contain(SharedTemplate).And.Contain(ConsumerTemplate)
            .And.NotContain(PublisherTemplate);
        set.Accounting.Shared.Should().Equal(1, 2, 3);
        set.Accounting.IsComplete.Should().BeTrue();
    }
}
