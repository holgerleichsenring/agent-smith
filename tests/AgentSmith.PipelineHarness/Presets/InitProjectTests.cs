using AgentSmith.Application.Services;
using AgentSmith.Contracts.Providers;
using AgentSmith.PipelineHarness.Composition;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace AgentSmith.PipelineHarness.Presets;

/// <summary>
/// p0199d fast-tier init-project coverage. Exercises the full preset chain
/// (PipelineNameInitializer, CheckoutSource, AnalyzeCode,
/// PublishProjectLanguage, LoadSkills, BootstrapDiscover, BootstrapDispatch,
/// BootstrapRound, WriteRunResult, InitCommit) against the SkillsBackend.
/// Fixture catalog. BootstrapDiscover takes the re-init projection path
/// because StubSourceProvider surfaces an existing .agentsmith/contexts/
/// default tree; BootstrapDispatch then matches csharp-bootstrap by
/// project_language='csharp' and fans out one BootstrapRound. The scripted
/// LLM writes coding-principles.md so BootstrapRound's "0 changes" guard
/// stays green. StubProjectAnalyzer replaces the LLM-driven analyzer so the
/// ScriptedChatClient queue isn't consumed before BootstrapRound runs.
/// </summary>
[Trait("Category", "PipelineHarness")]
public sealed class InitProjectTests
{
    [Fact]
    public async Task InitProject_RealHandlerChain_PipelineGreen()
    {
        await using var harness = RealCompositionHarness.Build(
            FixturePaths.For(FixturePaths.Default),
            SandboxBackend.Stub, session: null, SkillsBackend.Fixture,
            ReplaceLlmDrivenAnalyzer);
        EnqueueBootstrapWrite(harness);

        var runner = new PipelineRunner(harness.Services);
        var result = await runner.RunAsync("init-project");

        result.IsSuccess.Should().BeTrue(
            $"init-project handler chain must complete with the fixture skill catalog: {result.Message}");
        harness.ChatClient.ToolCalls.First("write_file").StringArg("path")
            .Should().Contain("coding-principles.md",
                "BootstrapRound's only allowed write surface in the fixture is the principles file");
    }

    private static void ReplaceLlmDrivenAnalyzer(IServiceCollection services)
    {
        services.RemoveAll<IProjectAnalyzer>();
        services.AddSingleton<IProjectAnalyzer, StubProjectAnalyzer>();
    }

    private static void EnqueueBootstrapWrite(RealCompositionHarness harness)
    {
        harness.ChatClient
            .EnqueueToolCall("write_file",
                """{"path":"primary/.agentsmith/contexts/default/coding-principles.md","content":"# Harness fixture coding principles"}""")
            .EnqueueText("Bootstrap files written.");
    }
}
