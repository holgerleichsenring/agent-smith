using AgentSmith.Contracts.Providers;
using AgentSmith.PipelineHarness.Composition;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace AgentSmith.PipelineHarness.Presets;

/// <summary>
/// 2026-11-17-7c9be through the production composition: the branch carries a file an earlier pass committed
/// beside the last pass's write. The completion comment lists both — what the branch changed against its
/// base — where it used to list only the last pass's tool writes.
/// </summary>
[Trait("Category", "PipelineHarness")]
public sealed class CompletionCommentChangesTests
{

    [Fact]
    public async Task RealComposition_BranchCarriesAnEarlierCommit_TheCommentListsIt()
    {
        var tickets = new RecordingTicketProvider();
        await using var harness = RealCompositionHarness.Build(FixturePaths.For(FixturePaths.Default), services =>
        {
            HarnessProjectAnalyzerStub.Register(services);
            services.RemoveAll<ITicketProviderFactory>();
            services.AddSingleton<ITicketProviderFactory>(new RecordingTicketProviderFactory(tickets));
        });
        harness.StubSandboxFactory!.GitAnswers["diff --name-status"] = (0, "A\tsrc/EarlierPass.cs\nM\tsrc/Patch.cs\n");
        harness.ChatClient
            .EnqueueText("Planning: patch it.")
            .EnqueueToolCall("write_file", """{"path":"primary/src/Patch.cs","content":"// fix"}""")
            .EnqueueFinish();

        var result = await new PipelineRunner(harness.Services).RunAsync("code");

        result.IsSuccess.Should().BeTrue(result.Message);
        tickets.Finalized.Should().ContainSingle().Which.Comment
            .Should().Contain("src/EarlierPass.cs", "the branch carries it, though the last pass never wrote it")
            .And.Contain("src/Patch.cs");
    }
}
