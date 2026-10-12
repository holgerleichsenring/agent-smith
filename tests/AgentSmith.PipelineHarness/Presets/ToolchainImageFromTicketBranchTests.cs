using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.PipelineHarness.Composition;
using AgentSmith.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace AgentSmith.PipelineHarness.Presets;

/// <summary>
/// 2026-10-12-6bfaa fast-tier end-to-end: a Scala project whose context.yaml exists only on the
/// ticket branch, with the operator's sandbox.images.scala configured. Live, the run read the
/// default branch, found no context and started every sandbox on the generic fallback image —
/// no JDK, no sbt. The sandbox must start on the configured image.
/// </summary>
[Trait("Category", "PipelineHarness")]
public sealed class ToolchainImageFromTicketBranchTests
{
    private const string ScalaImage = "sbtscala/scala-sbt:eclipse-temurin-17.0.4_1.7.1_3.2.0";
    private const string ScopeReply =
        """{"repos":[{"name":"primary","affected":true,"confidence":0.95}],"rationale":"the only repo"}""";
    private const string ContextYaml =
        "meta:\n  workdir: .\n  project: app\n  purpose: a Scala service\nstack:\n  lang: scala\n";

    [Fact]
    public async Task Harness_ContextsOnlyOnTicketBranch_SandboxGetsConfiguredImage()
    {
        await using var harness = RealCompositionHarness.Build(FixturePaths.For(FixturePaths.Default), services =>
        {
            HarnessProjectAnalyzerStub.Register(services);
            services.RemoveAll<ISourceProviderFactory>();
            services.AddSingleton<ISourceProviderFactory>(
                new TicketBranchContextSourceProviderFactory("agent-smith/1", ContextYaml));
        });
        harness.ChatClient.EnqueueScopeReply(ScopeReply);
        var runner = new PipelineRunner(harness.Services)
        {
            SandboxOverride = new SandboxConfig { Images = new Dictionary<string, string> { ["scala"] = ScalaImage } },
        };

        await runner.RunAsync("code");

        harness.StubSandboxFactory!.Spawned.Should().NotBeEmpty()
            .And.OnlyContain(s => s.Spec.ToolchainImage == ScalaImage,
                "the ticket branch's context.yaml names scala, and the operator configured its image");
    }
}
