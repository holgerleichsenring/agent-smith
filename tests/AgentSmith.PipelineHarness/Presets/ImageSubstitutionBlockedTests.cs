using AgentSmith.Contracts.Providers;
using AgentSmith.PipelineHarness.Composition;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace AgentSmith.PipelineHarness.Presets;

/// <summary>
/// 2026-10-12-50bda fast-tier end-to-end: a Scala context declares a stack.image outside the
/// trusted registries and an sbt build stage. Live, the chain dropped the image, the sandbox ran
/// the generic image, and the run failed at verify 25 minutes later with 'sbt: not found'. The run
/// must stop at preflight, naming the image and the levers, before any master step.
/// </summary>
[Trait("Category", "PipelineHarness")]
public sealed class ImageSubstitutionBlockedTests
{
    private const string RefusedImage = "sbtscala/scala-sbt:eclipse-temurin-25_1.11.7_2.13.18";
    private const string ScopeReply =
        """{"repos":[{"name":"primary","affected":true,"confidence":0.95}],"rationale":"the only repo"}""";

    [Fact]
    public async Task Harness_RefusedContextImageWithVerifyStages_RunStopsAtPreflight()
    {
        var yaml = "meta:\n  workdir: .\n  project: app\n  purpose: a Scala service\nstack:\n  lang: scala\n"
            + $"  image: {RefusedImage}\nverify:\n  - label: build\n    command: sbt compile\n";
        await using var harness = RealCompositionHarness.Build(FixturePaths.For(FixturePaths.Default), services =>
        {
            HarnessProjectAnalyzerStub.Register(services);
            services.RemoveAll<ISourceProviderFactory>();
            services.AddSingleton<ISourceProviderFactory>(
                new TicketBranchContextSourceProviderFactory("agent-smith/1", yaml));
        });
        harness.ChatClient.EnqueueScopeReply(ScopeReply);
        harness.StubSandboxFactory!.NamesImages = true;

        var result = await new PipelineRunner(harness.Services).RunAsync("code");

        result.IsSuccess.Should().BeFalse("the run must not work in an image substituted for the declared one");
        result.Message.Should().Contain("Preflight failed").And.Contain("context-image-trust")
            .And.Contain(RefusedImage).And.Contain("'sbtscala/'").And.Contain("Trusted image registries");
        harness.ChatClient.InvocationCount.Should().BeLessThanOrEqualTo(1,
            "at most the scope classifier spoke; no analyzer and no master ran past the preflight");
    }
}
