using AgentSmith.Application.Services.Specs.Revision;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Specs;
using AgentSmith.PipelineHarness.Composition;
using AgentSmith.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace AgentSmith.PipelineHarness.Presets;

/// <summary>
/// 2026-11-27-3532d fast-tier end-to-end: the coding master finds its spec cannot be implemented as written, retires it
/// with revise_spec(retire) and builds the replacement it wrote. The rest of the retired spec's block verifies and records
/// the replacement; the next spec, which required the retired one, requires the replacement and runs after it; the
/// retired spec stays on the branch as written and settled, and the run ends green.
/// </summary>
[Trait("Category", "PipelineHarness")]
public sealed class RetireSpecCompositionTests
{
    private const string StagedNames = "diff --cached --name-only";
    private const string Retired = HarnessSeries.Base + "a";
    private const string Next = HarnessSeries.Base + "b";
    private const string Replacement = HarnessSeries.Base + "c";

    private static readonly HarnessSeries.Member[] Series =
    [
        new("Introduce the empty-payload guard", "An empty request body is answered with 400."),
        new("Move the existing callers onto the guard", "No caller builds its own empty-payload check.", Step: "callers", Requires: 0),
    ];

    [Fact]
    public async Task Harness_TheMasterRetiresItsSpec_TheReplacementIsBuiltAndRecordedAndTheRetiredOneKept()
    {
        await using var harness = Harness();
        harness.ChatClient
            // spec a: the master reads that the guard belongs in the middleware, and retires the spec for that one
            .EnqueueText("Planning: the handler cannot see the body; the middleware can.")
            .EnqueueToolCall("write_file", """{"path":"primary/src/Middleware.cs","content":"// guard"}""")
            .EnqueueDeferred(() =>
            {
                harness.StubSandboxFactory!.GitAnswers[StagedNames] = (0, string.Empty);
                return RetireCall();
            })
            .EnqueueDeferred(() =>
            {
                harness.StubSandboxFactory!.GitAnswers.Remove(StagedNames);
                return new ChatResponse(new ChatMessage(ChatRole.Assistant, "Retired; building the replacement."));
            })
            .EnqueueReport()
            // spec b: its own pass, after the replacement
            .EnqueueText("Planning: move the callers.")
            .EnqueueToolCall("write_file", """{"path":"primary/src/Callers.cs","content":"// moved"}""")
            .EnqueueReport();
        var runner = new PipelineRunner(harness.Services) { DoneStatus = "done", FailedStatus = "failed" }.WithSeries(Series);
        runner.RequestContext[ContextKeys.ProjectName] = "fixture";

        var result = await runner.RunAsync("code");

        result.IsSuccess.Should().BeTrue(result.Message);
        var context = runner.LastContext!;
        var set = context.Get<SpecSet>(ContextKeys.SpecSet);
        set.Specs.Select(s => s.SpecId).Should().Equal(Retired, Replacement, Next);
        set.Retired().Should().Equal(Retired);
        set.Executed.Should().Equal(Replacement, Next);
        set.Specs[2].Draft.Requires.Should().Equal(Replacement);
        set.Specs[1].Draft.Supersedes.Should().Equal(Retired);
        set.Revisions.Should().Contain(r => r.Cause.Contains($"{Retired} superseded by {Replacement}", StringComparison.Ordinal));
        context.Get<SpecSequenceProgress>(ContextKeys.SpecSequenceProgress).Specs.Select(p => (p.SpecId, p.State)).Should().Equal(
            (Retired, SpecRunState.Superseded), (Replacement, SpecRunState.Done), (Next, SpecRunState.Done));
        var writes = harness.StubSandboxFactory!.Spawned.SelectMany(s => s.Sandbox.RanSteps)
            .Where(s => s.Kind == AgentSmith.Sandbox.Wire.StepKind.WriteFile && s.Path is not null).Select(s => s.Path!).ToList();
        writes.Should().Contain(p => p.Contains($".agentsmith/specs/done/{Replacement}", StringComparison.Ordinal));
        writes.Should().NotContain(p => p.Contains($".agentsmith/specs/done/{Retired}", StringComparison.Ordinal),
            "a retired spec never ran, so it is never recorded done");
        writes.Should().Contain(p => p.Contains($".agentsmith/specs/planned/{Retired}", StringComparison.Ordinal));
    }

    private static ChatResponse RetireCall()
    {
        var yaml = $"spec: {Retired}\ngoal: \"Guard empty payloads in the request middleware\"\nrepos: [primary]\nsteps:\n"
            + "  - id: middleware\n    action: \"Answer an empty body with 400 in the middleware\"\ndone:\n"
            + "  - \"An empty request body is answered with 400 by the middleware.\"\n";
        var args = new Dictionary<string, object?>
        {
            ["spec_yaml"] = yaml, ["reason"] = "the handler never sees the raw body, so the guard belongs in the middleware",
            ["retire"] = true,
        };
        return new ChatResponse(new ChatMessage(ChatRole.Assistant, [new FunctionCallContent("call_retire", ReviseSpecToolHost.Name, args)]));
    }

    private static RealCompositionHarness Harness()
    {
        var harness = RealCompositionHarness.Build(FixturePaths.For(FixturePaths.Default), services =>
        {
            HarnessProjectAnalyzerStub.Register(services);
            services.RemoveAll<ITicketProviderFactory>();
            services.AddSingleton<ITicketProviderFactory>(new RecordingTicketProviderFactory(new RecordingTicketProvider()));
        });
        harness.StubSandboxFactory!.AdvancesHeadOnCommit = true; // the retirement is a commit of its own
        return harness;
    }
}
