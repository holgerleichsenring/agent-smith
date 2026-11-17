using AgentSmith.Contracts.Commands;
using AgentSmith.PipelineHarness.Composition;
using FluentAssertions;

namespace AgentSmith.PipelineHarness.Presets;

/// <summary>
/// 2026-11-17-023fb: the refresh-base preset through the real composition — the server's string seeds reach the step
/// through its builder, the checkout gives it a sandbox, and the step answers for the branch it was handed; a branch
/// outside agent-smith/ is refused before anything is written.
/// </summary>
[Trait("Category", "PipelineHarness")]
public sealed class RefreshBaseTests
{
    [Fact]
    public async Task RefreshBase_SeededWorkBranch_RunsThroughTheStep()
    {
        var result = await RefreshAsync("agent-smith/77");

        result.IsSuccess.Should().BeTrue(result.Message);
    }

    [Fact]
    public async Task RefreshBase_AForeignBranch_IsRefused()
    {
        var result = await RefreshAsync("feature/someone");

        result.IsSuccess.Should().BeFalse();
        result.Message.Should().Contain("not an agent-smith work branch");
    }

    private static async Task<AgentSmith.Domain.Models.CommandResult> RefreshAsync(string branch)
    {
        await using var harness = RealCompositionHarness.Build(FixturePaths.For(FixturePaths.Default));
        var runner = new PipelineRunner(harness.Services);
        runner.RequestContext[ContextKeys.CheckoutBranch] = branch;
        runner.RequestContext[ContextKeys.BaseRefreshPrUrl] = "https://stub.test/primary/pull/5";
        runner.RequestContext[ContextKeys.BaseRefreshPrNumber] = "5";
        runner.RequestContext[ContextKeys.BaseRefreshBaseBranch] = "main";
        return await runner.RunAsync(PipelinePresets.RefreshBaseName);
    }
}
