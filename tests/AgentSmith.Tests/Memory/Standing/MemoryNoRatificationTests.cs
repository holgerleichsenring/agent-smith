using AgentSmith.Application.Services.Loop;
using AgentSmith.Application.Services.Tools;
using AgentSmith.Contracts.Commands;
using AgentSmith.Tests.Events;
using AgentSmith.Tests.Handlers;
using AgentSmith.Tests.Specs;
using AgentSmith.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.Extensions.AI;

namespace AgentSmith.Tests.Memory.Standing;

/// <summary>
/// 2026-10-25-9324e: no memory waits for a person — remember writes no status and publishes no proposal, and a
/// master whose skill carries {MemoryIndexSection} gets the index with its standing, a scan master included.
/// </summary>
public sealed class MemoryNoRatificationTests
{
    private const string EntryPath = "/work/.agentsmith/memory/ask-first.md";
    private const string IndexPath = "/work/.agentsmith/memory/MEMORY.md";

    [Fact]
    public async Task Remember_Feedback_WritesNoProposedStatus()
    {
        var reader = new InMemorySandboxFileReader();
        var host = new MemoryWriteToolHost(TestRepositories.One(reader, "shop"), "/work");

        var answer = await host.Remember("feedback", "Ask First", "Ask before guessing", "The body.");

        answer.Should().Contain("recorded in shop (feedback)").And.Contain("hint")
            .And.NotContain("PROPOSED").And.NotContain("ratif");
        reader.Files[EntryPath].Should().NotContain("status:");
        reader.Files[IndexPath].Should().Contain("- [ask-first](ask-first.md) (feedback) — Ask before guessing");
    }

    [Fact]
    public async Task SecurityScanMaster_RendersMemoryIndex()
    {
        var loop = new PromptCapture();
        var context = MasterHandlerFixture.BuildContext("security-master");
        context.Pipeline.Set(ContextKeys.MemoryIndex, "- [fp-x](fp-x.md) (feedback, holding · no record yet) — a known false positive");

        await MasterHandlerFixture.Build(loop, new MasterHandlerFixture.StubPromptCatalog("security-master",
                "Scan.\n{MemoryIndexSection}"), "observation")
            .ExecuteAsync(context, CancellationToken.None);

        loop.Prompts.Should().Contain("## Experiential memory index").And.Contain("a hint with a track record")
            .And.Contain("[fp-x](fp-x.md)").And.NotContain("ratif");
    }

    [Fact]
    public async Task CodingMasterOfASpec_BodyPlaceholderStaysEmpty()
    {
        var loop = new PromptCapture();
        var context = MasterHandlerFixture.BuildContext("coding-agent-master");
        context.Pipeline.Set(ContextKeys.CurrentSpec, SpecBuildNoteTests.Draft("2026-10-25-0001a"));
        context.Pipeline.Set(ContextKeys.MemoryIndex, "- [once](once.md) (project) — read once");

        await MasterHandlerFixture.Build(loop, new MasterHandlerFixture.StubPromptCatalog("coding-agent-master",
                "Code.\n{MemoryIndexSection}"))
            .ExecuteAsync(context, CancellationToken.None);

        loop.System.Should().Contain("Code.").And.NotContain("## Experiential memory index",
            "a spec's master reads the index in the spec-execution prompt, not twice");
    }

    private sealed class PromptCapture : IAgenticLoopRunner
    {
        public string Prompts { get; private set; } = string.Empty;

        public string System { get; private set; } = string.Empty;

        public Task<AgenticLoopResult> RunAsync(AgenticLoopRequest request, CancellationToken cancellationToken)
        {
            if (Prompts.Length == 0) (Prompts, System) = (request.SystemPrompt + "\n" + request.UserPrompt, request.SystemPrompt);
            return Task.FromResult(new AgenticLoopResult(
                new ChatResponse(new ChatMessage(ChatRole.Assistant, "No findings."))
                {
                    Usage = new UsageDetails { InputTokenCount = 10, OutputTokenCount = 5 },
                },
                TimeSpan.FromSeconds(1)));
        }
    }
}
