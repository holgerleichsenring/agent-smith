using AgentSmith.PipelineHarness.Composition;
using FluentAssertions;

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
            HarnessProjectAnalyzerStub.Register);
        EnqueueBootstrapWrite(harness);

        var runner = new PipelineRunner(harness.Services);
        var result = await runner.RunAsync("init-project");

        result.IsSuccess.Should().BeTrue(
            $"init-project handler chain must complete with the fixture skill catalog: {result.Message}");
        harness.ChatClient.ToolCalls.First("write_file").StringArg("path")
            .Should().Contain("coding-principles.md",
                "BootstrapRound's only allowed write_file surface in the fixture is the principles file");
        harness.ChatClient.ToolCalls.First("write_context_yaml").StringArg("context_name")
            .Should().Be("default",
                "context.yaml must go through the typed write_context_yaml tool (p0193)");
    }

    private static void EnqueueBootstrapWrite(RealCompositionHarness harness)
    {
        harness.ChatClient
            .EnqueueToolCall("write_file",
                """{"path":"primary/.agentsmith/contexts/default/coding-principles.md","content":"# Harness fixture coding principles"}""")
            // p0193-fix: BootstrapRound fails loudly unless context.yaml exists on
            // the sandbox after the round — script the typed write path too.
            .EnqueueToolCall("write_context_yaml",
                """{"repo":"","context_name":"default","document":{"meta":{"workdir":"."},"stack":{"lang":"csharp"}}}""")
            .EnqueueText("Bootstrap files written.");
    }
}
