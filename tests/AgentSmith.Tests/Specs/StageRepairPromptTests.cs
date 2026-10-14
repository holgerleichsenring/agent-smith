using AgentSmith.Application.Services;
using AgentSmith.Application.Services.Specs;
using AgentSmith.Application.Services.Tools;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Models;
using FluentAssertions;

namespace AgentSmith.Tests.Specs;

/// <summary>
/// The spec prompt names every declared stage and the tool that runs it, and a repair pass
/// caused by a red stage does not claim an account that was never taken.
/// </summary>
public sealed class StageRepairPromptTests
{
    private const string AlreadyAccounted = "The rest of the spec is already accounted for";

    [Fact]
    public void SpecExecutionPrompt_StageRepair_NamesTheVerificationAndCarriesTheOutput()
    {
        var pipeline = new PipelineContext();
        pipeline.Set(ContextKeys.OutstandingCriteria, new List<string>
        {
            $"{StageRepair.ItemPrefix} shop-node test 'pnpm test' exited 1 (run at /work). Its output ends:\nFAIL src/app.test.ts",
        });

        var block = SpecExecutionPromptBlocks.OutstandingCriteria(pipeline);

        block.Should().Contain("The verification").And.Contain("FAIL src/app.test.ts")
            .And.Contain("No criterion has been accounted for yet").And.NotContain(AlreadyAccounted);
    }

    [Fact]
    public void SpecExecutionPrompt_AccountRepair_KeepsTheAlreadyAccountedForSentence()
    {
        var pipeline = new PipelineContext();
        pipeline.Set(ContextKeys.OutstandingCriteria, new List<string> { "shop: the export lists every order" });

        SpecExecutionPromptBlocks.OutstandingCriteria(pipeline).Should().Contain(AlreadyAccounted);
    }

    [Fact]
    public void SpecExecutionPrompt_DeclaredStages_NamesRunGateStageAndEveryContextLabelPair()
    {
        var pipeline = Pipeline();
        pipeline.Set<IReadOnlyDictionary<string, string>>(ContextKeys.SandboxRepos,
            new Dictionary<string, string> { ["shop-dotnet"] = "shop", ["shop-node"] = "shop" });
        pipeline.Set<IReadOnlyDictionary<string, IReadOnlyList<RemoteContextDiscovery>>>(ContextKeys.SandboxContexts,
            new Dictionary<string, IReadOnlyList<RemoteContextDiscovery>>
            {
                ["shop-dotnet"] = [new("default", ".", "csharp", Verify: [new("build", "dotnet build"), new("test", "dotnet test")])],
                ["shop-node"] = [new("dashboard", "web", "typescript", Verify: [new("test", "pnpm --dir web test")])],
            });

        var prompt = Build(pipeline);

        prompt.Should().Contain(GateStageToolHost.Name).And.Contain("verification runs exactly these")
            .And.Contain("- shop default/build").And.Contain("- shop default/test").And.Contain("- shop dashboard/test");
    }

    [Fact]
    public void SpecExecutionPrompt_NoDeclaredStage_RendersNoGateBlock()
    {
        var prompt = Build(Pipeline());

        prompt.Should().NotContain(GateStageToolHost.Name).And.NotContain("Declared gate stages");
        SpecExecutionPromptBlocks.DeclaredGate(Pipeline()).Should().BeEmpty();
    }

    private static PipelineContext Pipeline()
    {
        var pipeline = new PipelineContext();
        pipeline.Set(ContextKeys.CurrentSpec, new SpecDraft("2026-10-13-8762c", "goal", "spec: 2026-10-13-8762c", []));
        return pipeline;
    }

    private static string Build(PipelineContext pipeline) =>
        new SpecExecutionPromptFactory().Build(pipeline,
            new Ticket(new TicketId("7"), "title", "body", null, "open", "test"),
            new Repository(new BranchName("agent-smith/7"), "/work"), ["shop-dotnet"], string.Empty, string.Empty);
}
