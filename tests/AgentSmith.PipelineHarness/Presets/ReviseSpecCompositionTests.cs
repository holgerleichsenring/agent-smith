using System.Text.Json;
using AgentSmith.Application.Services.Specs.Revision;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Specs;
using AgentSmith.Contracts.Turns;
using AgentSmith.PipelineHarness.Composition;
using AgentSmith.PipelineHarness.Llm;
using AgentSmith.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace AgentSmith.PipelineHarness.Presets;

/// <summary>
/// 2026-11-17-ec48a fast-tier end-to-end: the operator writes to a running code attempt, the message reaches the master
/// with a tool result, and the master revises the spec it is building with revise_spec instead of pausing. The revision
/// is committed to the ticket branch under the run's cause, and the run is judged against the revised done criteria.
/// 2026-11-19-3e10: no reviewer judges the master's text.
/// </summary>
[Trait("Category", "PipelineHarness")]
public sealed class ReviseSpecCompositionTests
{
    private const string Criterion = "An empty request body is answered with 400.";
    private const string Added = "A whitespace-only request body is answered with 400.";
    private const string Message = "also answer a whitespace-only body with 400";
    private const string StagedNames = "diff --cached --name-only";



    private sealed class OneMessage : ITicketConversationMessages
    {
        public Task<DateTimeOffset?> LastActivityAsync(string project, string ticketId, CancellationToken ct) =>
            Task.FromResult<DateTimeOffset?>(DateTimeOffset.UtcNow);

        public Task<IReadOnlyList<RunAddressedMessage>> AddressedToAsync(
            string project, string ticketId, string runId, int afterIndex, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<RunAddressedMessage>>(afterIndex < 4
                ? [new RunAddressedMessage(4, Message, DateTimeOffset.UtcNow)]
                : []);
    }

    [Fact]
    public async Task Harness_AMessageChangesTheCriteria_TheMasterRevisesItsSpecWithoutAReviewAndTheRunEndsGreen()
    {
        await using var harness = Harness();
        var callsBefore = 0;
        Script(harness, () => callsBefore = harness.ChatClient.CallMessages.Count, Verdict(Criterion, Added));
        var runner = Runner(harness);

        var result = await runner.RunAsync("code");

        result.IsSuccess.Should().BeTrue(result.Message);
        Results(harness).Should().ContainSingle(r => r.Contains("is revised: revision", StringComparison.Ordinal));
        harness.ChatClient.CallMessages.Count.Should().Be(callsBefore + 2,
            "2026-11-19-3e10: the master's next turn and its closing turn — no reviewer is asked between them");
        var context = runner.LastContext!;
        context.Get<SpecDraft>(ContextKeys.CurrentSpec).Done.Should().Equal(Criterion, Added);
        context.Get<SpecSet>(ContextKeys.SpecSet).Revisions.Should().Contain(r => r.Cause.StartsWith("revised in run ", StringComparison.Ordinal)
            && r.Cause.EndsWith(": the operator asked to also answer a whitespace-only body", StringComparison.Ordinal));
        var steps = harness.StubSandboxFactory!.Spawned.SelectMany(s => s.Sandbox.RanSteps).ToList();
        steps.Should().Contain(s => s.Kind == AgentSmith.Sandbox.Wire.StepKind.WriteFile && s.Path != null
            && s.Path.Contains(".agentsmith/specs/planned/", StringComparison.Ordinal) && s.Content != null && s.Content.Contains(Added));
        steps.Should().Contain(s => s.Command == "git" && s.Args != null && s.Args.Contains("commit")
            && s.Args.Any(a => a.Contains("(revised in run ", StringComparison.Ordinal)));
    }

    private static RealCompositionHarness Harness()
    {
        var harness = RealCompositionHarness.Build(FixturePaths.For(FixturePaths.Default), services =>
        {
            HarnessProjectAnalyzerStub.Register(services);
            services.RemoveAll<ITicketProviderFactory>();
            services.AddSingleton<ITicketProviderFactory>(new RecordingTicketProviderFactory(new RecordingTicketProvider()));
            services.AddSingleton<ITicketConversationMessages>(new OneMessage());
        });
        harness.StubSandboxFactory!.AdvancesHeadOnCommit = true; // the master's revision is a commit of its own
        return harness;
    }

    // The stub counts every written file as staged; a real write_file stages nothing. While the master revises, the
    // index is answered as git would, so the gate that keeps staged work out of the spec commit admits the revision.
    private static void Script(RealCompositionHarness harness, Action beforeRevise, string verdict) =>
        harness.ChatClient
            .EnqueueText("Planning: introduce the guard.")
            .EnqueueToolCall("write_file", """{"path":"primary/src/Guard.cs","content":"// guard"}""")
            .EnqueueDeferred(() =>
            {
                beforeRevise();
                harness.StubSandboxFactory!.GitAnswers[StagedNames] = (0, string.Empty);
                return ReviseCall(harness);
            })
            .EnqueueDeferred(() =>
            {
                harness.StubSandboxFactory!.GitAnswers.Remove(StagedNames);
                return new ChatResponse(new ChatMessage(ChatRole.Assistant, [new TextContent(verdict),
                    new FunctionCallContent("call_finish", "finish_spec",
                        new Dictionary<string, object?> { ["status"] = "green", ["summary"] = "done" })]));
            })
            .EnqueueText(string.Empty);

    // The master re-emits the spec its prompt carries, with the operator's criterion added.
    private static ChatResponse ReviseCall(RealCompositionHarness harness)
    {
        var prompt = harness.ChatClient.CallMessages[^1].Select(m => m.Text ?? string.Empty)
            .First(t => t.Contains("You are implementing SPEC", StringComparison.Ordinal));
        var yaml = System.Text.RegularExpressions.Regex.Match(prompt, "```yaml\\n(.*?)\\n```", System.Text.RegularExpressions.RegexOptions.Singleline).Groups[1].Value;
        var line = yaml.Split('\n').Single(l => l.Contains(Criterion, StringComparison.Ordinal));
        var revised = yaml.Replace(line, line + "\n" + line.Replace(Criterion, Added, StringComparison.Ordinal), StringComparison.Ordinal);
        var args = new Dictionary<string, object?>
        {
            ["spec_yaml"] = revised, ["reason"] = "the operator asked to also answer a whitespace-only body",
        };
        return new ChatResponse(new ChatMessage(ChatRole.Assistant,
            [new FunctionCallContent("call_revise", ReviseSpecToolHost.Name, args)]));
    }

    // 2026-11-18-98fa: the master's markdown report, naming the done lines it saw.
    private static string Verdict(params string[] criteria) =>
        "## Delivered\n" + string.Join("\n", criteria.Select(c => $"- {c}: the guard"));


    private static List<string> Results(RealCompositionHarness harness) =>
        [.. harness.ChatClient.CallMessages.SelectMany(call => call).SelectMany(m => m.Contents).OfType<FunctionResultContent>()
            .Where(r => r.CallId == "call_revise").Select(r => r.Result?.ToString() ?? string.Empty).Distinct()];

    private static PipelineRunner Runner(RealCompositionHarness harness)
    {
        var runner = new PipelineRunner(harness.Services) { DoneStatus = "done", FailedStatus = "failed" }
            .WithSeries(new HarnessSeries.Member("Introduce the empty-payload guard", "An empty request body is answered with 400."));
        runner.RequestContext[ContextKeys.ProjectName] = "fixture";
        return runner;
    }
}
