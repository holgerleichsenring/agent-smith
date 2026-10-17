using System.Text.Json;
using AgentSmith.Application.Models;
using AgentSmith.Application.Services.Handlers;
using AgentSmith.Application.Services.Loop;
using AgentSmith.Application.Services.Loop.Sanity;
using AgentSmith.Server.Services;
using FluentAssertions;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentSmith.Tests.Handlers;

/// <summary>Every function on the master's and the children's surface carries the sanity annotations.</summary>
[Trait("TestProcess", "env-1")]
[Collection(TestSupport.EnvVarCollection.Name)]
public sealed class SanitySurfaceTests
{
    [Fact]
    public async Task MasterToolComposition_MasterAndChildTools_AreWrapped()
    {
        var services = new ServiceCollection();
        services.AddLogging(builder => builder.AddProvider(NullLoggerProvider.Instance));
        ServerCompositionBuilder.ConfigureServices(services, "agentsmith.yml");
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        scope.ServiceProvider.GetRequiredService<MasterToolComposition>().Should().NotBeNull();
        scope.ServiceProvider.GetServices<IToolResultSanityRule>().Select(r => r.GetType()).Should().Equal(
            typeof(EmptySubAgentAnswerRule), typeof(UncitedZeroHitRule), typeof(OutsideWorkCommandRule));
        var wrapped = scope.ServiceProvider.GetRequiredService<ToolResultSanityWrapper>()
            .Wrap([AIFunctionFactory.Create(() => "ok", name: "run_command")], new Contracts.Commands.PipelineContext());
        wrapped.Single().Should().BeOfType<SanityAnnotatedTool>();

        var loop = new CapturingLoop();
        var children = new MasterHandlerFixture.StubSubAgentRunner();
        await MasterHandlerFixture.Build(loop, new MasterHandlerFixture.StubPromptCatalog("coding-agent-master", "body"),
                maxSubAgents: 20, subAgents: children)
            .ExecuteAsync(MasterHandlerFixture.BuildContext("coding-agent-master"), CancellationToken.None);

        loop.MasterTools.Should().NotBeEmpty().And.OnlyContain(t => t is SanityAnnotatedTool);
        loop.MasterTools.Select(t => t.Name).Should().Contain(["spawn_agents", "read_sub_agent_observations", "write_file"]);
        children.SeenContexts.Single().ChildTools.Should().NotBeEmpty().And.OnlyContain(t => t is SanityAnnotatedTool);
    }

    /// <summary>2026-10-17-a196a, through the server's real container: a server spec's write into the worker
    /// repository never reaches the tool.</summary>
    [Fact]
    public async Task Harness_WrappedWriteTool_RefusesTheUnnamedRepository()
    {
        var services = new ServiceCollection();
        services.AddLogging(builder => builder.AddProvider(NullLoggerProvider.Instance));
        ServerCompositionBuilder.ConfigureServices(services, "agentsmith.yml");
        using var provider = services.BuildServiceProvider();
        var pipeline = new Contracts.Commands.PipelineContext();
        pipeline.Set<IReadOnlyDictionary<string, string>>(Contracts.Commands.ContextKeys.SandboxRepos,
            new Dictionary<string, string> { ["Sample.Server"] = "Sample.Server", ["Sample.Worker"] = "Sample.Worker" });
        pipeline.Set(Contracts.Commands.ContextKeys.CurrentSpec,
            new Contracts.Models.SpecDraft("2026-10-17-0000a", "goal", "spec: x", []) { Repos = ["Sample.Server"] });
        var wrote = false;
        var tool = (AIFunction)provider.GetRequiredService<ToolResultSanityWrapper>().Wrap(
            [AIFunctionFactory.Create((string path) => { wrote = true; return "Wrote"; }, name: "write_file")], pipeline).Single();

        var result = await tool.InvokeAsync(new AIFunctionArguments { ["path"] = "Sample.Worker/src/Queue.cs" });

        wrote.Should().BeFalse("the refusal stands before the tool");
        result!.ToString().Should().StartWith("Error: write_file into src/Queue.cs of repository 'Sample.Worker' was refused");
        RefusedWriteLog.Of(pipeline).Refused.Should().ContainSingle();
    }

    /// <summary>Records the master's tools and spawns one child so its surface is seen.</summary>
    private sealed class CapturingLoop : IAgenticLoopRunner
    {
        private const string OneTask = "[{\"name\":\"RepoScout\",\"activity\":\"read the sources\","
            + "\"task_description\":\"answer one question\",\"inherited_context\":"
            + "{\"pipeline_goal\":\"build it\",\"prior_context_slice\":\"none\"}}]";

        public IReadOnlyList<AIFunction> MasterTools { get; private set; } = [];

        public async Task<AgenticLoopResult> RunAsync(AgenticLoopRequest request, CancellationToken cancellationToken)
        {
            if (MasterTools.Count == 0)
            {
                MasterTools = [.. request.Tools!.OfType<AIFunction>()];
                await MasterTools.Single(t => t.Name == "spawn_agents").InvokeAsync(
                    new AIFunctionArguments { ["tasks"] = JsonDocument.Parse(OneTask).RootElement }, cancellationToken);
            }
            return new AgenticLoopResult(
                new ChatResponse(new ChatMessage(ChatRole.Assistant, "An answer with no outcome block."))
                {
                    Usage = new UsageDetails { InputTokenCount = 10, OutputTokenCount = 5 },
                },
                TimeSpan.FromSeconds(1));
        }
    }
}
