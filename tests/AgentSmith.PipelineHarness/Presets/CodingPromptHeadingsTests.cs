using AgentSmith.Application.Prompts;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Services;
using AgentSmith.PipelineHarness.Composition;
using AgentSmith.PipelineHarness.Evals;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace AgentSmith.PipelineHarness.Presets;

/// <summary>
/// 2026-10-12-967ad: the coding agent's prompt as the pinned catalog composes it. The backend
/// headed the spec "## Phase 2 of 3" while the coding-agent-master's own steps were "## Phase 1"
/// to "## Phase 4", so one prompt held two different "Phase 2" headings. With the spec saying
/// spec and the skill naming its steps, each heading means one thing.
/// <para>
/// The harness stubs the master's body by default; this run restores the embedded catalog and
/// the real prompt catalog, so the text under test is the text a deployed binary sends.
/// </para>
/// </summary>
[Trait("Category", "PipelineHarness")]
public sealed class CodingPromptHeadingsTests
{
    private const string GreenVerdict =
        """Done. {"status":"green","build_ran":true,"build_passed":true,"tests_ran":true,"tests_passed":true,"summary":"guarded","acceptance":[{"criterion":"criterion 1","status":"met","evidence":"handled"},{"criterion":"criterion 2","status":"met","evidence":"preserved"}]}""";

    [Fact]
    public async Task Harness_CodingPrompt_HasOneHeadingPerMeaning()
    {
        await using var harness = BuildHarness(new RecordingTicketProvider());
        harness.ChatClient
            .EnqueueText(SpecDerivationFixture.DerivationJson)
            .EnqueueToolCall("write_file", """{"path":"primary/src/Guard.cs","content":"// guard"}""")
            .EnqueueText(GreenVerdict);

        var runner = new PipelineRunner(harness.Services) { DoneStatus = "done", FailedStatus = "failed" };
        await runner.RunAsync("code");

        var coding = harness.ChatClient.PromptsSeen
            .FirstOrDefault(p => p.Contains("## Spec contract", StringComparison.Ordinal));
        coding.Should().NotBeNull("the coding master is asked once the spec is selected");
        var headings = coding!.Split('\n').Select(l => l.TrimEnd()).Where(l => l.StartsWith('#')).ToList();

        headings.Should().NotContain(h => h.Contains("Phase", StringComparison.OrdinalIgnoreCase),
            "the spec is a spec, and the skill's own steps carry names");
        headings.Should().ContainSingle(h => h == "## Execute",
            "the step heading appears once, and nothing else is headed like it");
        headings.Should().Contain(h => h.StartsWith("## Spec 1 of 1: ", StringComparison.Ordinal))
            .And.Contain("## Spec contract")
            .And.Contain("## Validate the plan").And.Contain("## Build & test").And.Contain("## Your verdict");
    }

    private static RealCompositionHarness BuildHarness(RecordingTicketProvider tickets) =>
        RealCompositionHarness.Build(FixturePaths.For(FixturePaths.Default), services =>
        {
            HarnessProjectAnalyzerStub.Register(services);
            services.RemoveAll<ITicketProviderFactory>();
            services.AddSingleton<ITicketProviderFactory>(new RecordingTicketProviderFactory(tickets));
            services.RemoveAll<ISkillsCatalogPath>();
            services.AddSingleton<ISkillsCatalogPath, EmbeddedSkillsCatalogPath>();
            services.RemoveAll<IPromptCatalog>();
            services.AddPromptCatalog();
        });
}
