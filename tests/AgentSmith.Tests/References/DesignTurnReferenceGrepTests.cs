using System.Text;
using AgentSmith.Application.Models;
using AgentSmith.Application.Services.Loop;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Domain.Models;
using AgentSmith.Tests.Handlers;
using AgentSmith.Tests.Sandbox;
using FluentAssertions;
using Microsoft.Extensions.AI;
using Moq;

namespace AgentSmith.Tests.References;

/// <summary>
/// 2026-10-01-283dc, the done line: a scripted design turn greps a colour out of an uploaded
/// stylesheet by its <c>reference:</c> address — through the master's own tool surface, the path
/// router and the real reference sandbox — and its prompt says what that address is.
/// </summary>
public sealed class DesignTurnReferenceGrepTests
{
    private const string DesignMaster = "design-partner-master";
    private const string Address = "reference:site";

    [Fact]
    public async Task DesignTurn_GrepByReferenceAddress_FindsTheStylesheetsColour()
    {
        var fixture = new ReferenceSandboxFixture();
        fixture.Set.Add(new ReferenceSetFile("site/css/site.css", Encoding.UTF8.GetBytes("h1 { color: #c0ffee; }")));
        await using var reference = fixture.Open(Holds.None(), Address);
        var loop = new GrepLoop();

        await MasterHandlerFixture.Build(loop, new MasterHandlerFixture.StubPromptCatalog(DesignMaster, "body"),
                subAgents: new MasterHandlerFixture.StubSubAgentRunner(), limits: new LoopLimitsConfig())
            .ExecuteAsync(Context(reference), CancellationToken.None);

        loop.Found.Should().Contain("site/css/site.css").And.Contain("#c0ffee");
        loop.SystemPrompt.Should().Contain("## Websites the operator uploaded").And.Contain($"`{Address}`");
    }

    private static AgenticMasterContext Context(ISandbox reference)
    {
        var context = MasterHandlerFixture.BuildContext(DesignMaster, includeTicket: false);
        var sandboxes = new Dictionary<string, ISandbox>(StringComparer.Ordinal)
        {
            ["repo-a"] = new Mock<ISandbox>().Object,
            [Address] = reference,
        };
        context.Pipeline.Set<IReadOnlyDictionary<string, ISandbox>>(ContextKeys.Sandboxes, sandboxes);
        context.Pipeline.Set<IReadOnlyDictionary<string, string>>(ContextKeys.SandboxRepos,
            sandboxes.Keys.ToDictionary(k => k, k => k, StringComparer.Ordinal));
        context.Pipeline.Set(ContextKeys.PipelineName, PipelinePresets.SpecDialogName);
        context.Pipeline.Set<IReadOnlyList<SpecDialogTurn>>(ContextKeys.SpecDialogTranscript,
        [
            new SpecDialogTurn(SpecDialogTurn.UserRole, "make the heading look like our site"),
            new SpecDialogTurn(SpecDialogTurn.AssistantRole, "Which colour?", SpecDialogTurnKind.Answer),
            new SpecDialogTurn(SpecDialogTurn.UserRole, "the one in the uploaded stylesheet"),
        ]);
        return context;
    }

    /// <summary>The model's one move: grep the uploaded site for a hex colour.</summary>
    private sealed class GrepLoop : IAgenticLoopRunner
    {
        public string Found { get; private set; } = string.Empty;

        public string SystemPrompt { get; private set; } = string.Empty;

        public async Task<AgenticLoopResult> RunAsync(AgenticLoopRequest request, CancellationToken cancellationToken)
        {
            if (SystemPrompt.Length == 0)
            {
                SystemPrompt = request.SystemPrompt + "\n" + request.UserPrompt;
                var grep = request.Tools.OfType<AIFunction>().Single(t => t.Name == "grep_in_tree");
                Found = (await grep.InvokeAsync(
                    new AIFunctionArguments { ["pattern"] = "#[0-9a-f]{6}", ["root"] = Address },
                    cancellationToken))?.ToString() ?? string.Empty;
            }
            return new AgenticLoopResult(
                new ChatResponse(new ChatMessage(ChatRole.Assistant, "The heading colour is #c0ffee."))
                {
                    Usage = new UsageDetails { InputTokenCount = 10, OutputTokenCount = 5 },
                },
                TimeSpan.FromSeconds(1));
        }
    }
}
