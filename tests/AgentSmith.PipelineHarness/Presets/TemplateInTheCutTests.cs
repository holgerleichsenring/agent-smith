using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.PipelineHarness.Composition;
using FluentAssertions;

namespace AgentSmith.PipelineHarness.Presets;

/// <summary>
/// 2026-09-13-84c0 through the REAL composition: a project that declares a template gets
/// that template in front of the derivation, named apart from its target repositories and
/// on its own look allowance. No LLM — the scripted client answers, and what matters is
/// the PROMPT it was handed.
/// </summary>
[Trait("Category", "PipelineHarness")]
public sealed class TemplateInTheCutTests
{
    [Fact]
    public async Task Harness_ProjectDeclaringATemplate_OffersItToTheDerivationApartFromTheTargets()
    {
        await using var harness = RealCompositionHarness.Build(
            FixturePaths.For(FixturePaths.Default), HarnessProjectAnalyzerStub.Register);
        harness.ChatClient.EnqueueText(SpecDerivationFixture.DerivationJson);

        var runner = new PipelineRunner(harness.Services)
        {
            TemplatesOverride = [new ProjectTemplate("default", "default", "v1.0.0", Reference())],
        };
        await runner.RunAsync("code");

        // The run makes several calls; the derivation is the one carrying the look section.
        var derivationPrompt = harness.ChatClient.PromptsSeen
            .Should().ContainSingle(prompt => prompt.Contains("## Repositories you may look into"))
            .Subject;
        derivationPrompt.Should().Contain("## Templates this project is built after");
        derivationPrompt.Should().Contain("template:default",
            "a template is addressed by a name a model can tell from a target");
        derivationPrompt.Should().Contain("answers HOW work is done here",
            "the precedence has to travel with the name, or the form gets copied from the wrong place");
    }

    [Fact]
    public async Task Harness_ProjectWithoutATemplate_PromptCarriesNoTemplateSection()
    {
        await using var harness = RealCompositionHarness.Build(
            FixturePaths.For(FixturePaths.Default), HarnessProjectAnalyzerStub.Register);
        harness.ChatClient.EnqueueText(SpecDerivationFixture.DerivationJson);

        var runner = new PipelineRunner(harness.Services);
        await runner.RunAsync("code");

        harness.ChatClient.PromptsSeen
            .Should().NotContain(prompt => prompt.Contains("Templates this project is built after"));
    }

    /// <summary>The repository a template lives in — read-only, and never the run's own.</summary>
    private static RepoConnection Reference() => new()
    {
        Name = "reference", Type = RepoType.Local,
        Path = "/tmp", Url = "https://stub.test/reference",
    };
}
