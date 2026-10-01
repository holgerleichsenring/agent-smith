using AgentSmith.Application.Models;
using AgentSmith.Application.Services.Loop;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Models;
using AgentSmith.Tests.DesignSources;
using FluentAssertions;
using Microsoft.Extensions.AI;

namespace AgentSmith.Tests.Handlers;

/// <summary>
/// 2026-10-01-7f7ad: a ticket's Figma link reaches the coding and phase-execution masters as a
/// rebuilt design reference; a scan master gets none.
/// </summary>
public sealed class DesignReferencePromptTests
{
    private const string Rebuilt = "https://www.figma.com/design/AbCdEf123456?node-id=1-2";

    [Fact]
    public async Task PhaseExecutionPrompt_TicketWithFigmaLink_CarriesTheSection()
    {
        var context = Context("coding-agent-master");
        context.Pipeline.Set(ContextKeys.ProjectConfig, new ResolvedProject
            { Name = "p", DesignSources = [new DesignSource("brand", DesignSourceVendor.Figma, "figma-token")] });
        context.Pipeline.Set(ContextKeys.PhaseSpec,
            new PhaseDraft("p1", "build the checkout", "phase: p1\ngoal: build the checkout\n", [])
            {
                Done = ["the checkout is built"],
            });

        var prompt = await UserPromptOf(context, "coding-agent-master");

        prompt.Should().Contain("## Design references").And.Contain(Rebuilt).And.Contain("read a frame with design_read");
    }

    [Fact]
    public async Task CodingPrompt_TicketWithFigmaLink_NoSource_SaysCannotBeRead()
    {
        var prompt = await UserPromptOf(Context("coding-agent-master"), "coding-agent-master");

        prompt.Should().Contain(Rebuilt).And.Contain("cannot be read here");
    }

    [Fact]
    public async Task ScanPrompt_TicketWithFigmaLink_HasNoSection()
    {
        var prompt = await UserPromptOf(Context("security-master"), "security-master", "observation");

        prompt.Should().NotContain("## Design references");
    }

    private static AgenticMasterContext Context(string master)
    {
        var context = MasterHandlerFixture.BuildContext(master, includeTicket: false);
        context.Pipeline.Set(ContextKeys.PipelineName, "code");
        context.Pipeline.Set(ContextKeys.Ticket, new Ticket(new TicketId("T-1"), "Checkout",
            $"Build the checkout as drawn: {FigmaFakes.Link}", null, "open", "test"));
        return context;
    }

    private static async Task<string> UserPromptOf(AgenticMasterContext context, string master, string? schema = null)
    {
        var loop = new FirstRequest();
        await MasterHandlerFixture.Build(loop, new MasterHandlerFixture.StubPromptCatalog(master, "body"), schema)
            .ExecuteAsync(context, CancellationToken.None);
        return loop.Seen!.UserPrompt;
    }

    private sealed class FirstRequest : IAgenticLoopRunner
    {
        public AgenticLoopRequest? Seen { get; private set; }

        public Task<AgenticLoopResult> RunAsync(AgenticLoopRequest request, CancellationToken cancellationToken)
        {
            Seen ??= request;
            return Task.FromResult(new AgenticLoopResult(
                new ChatResponse(new ChatMessage(ChatRole.Assistant, "done"))
                {
                    Usage = new UsageDetails { InputTokenCount = 10, OutputTokenCount = 5 },
                },
                TimeSpan.FromSeconds(1)));
        }
    }
}
