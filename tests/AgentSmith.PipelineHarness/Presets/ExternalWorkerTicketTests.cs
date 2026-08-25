using AgentSmith.Application.Models;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Events;
using AgentSmith.PipelineHarness.Composition;
using AgentSmith.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;

namespace AgentSmith.PipelineHarness.Presets;

/// <summary>
/// p0416: the end-to-end scenario this system has been missing — a whole mechanical
/// ticket driven by an EXTERNAL worker answering every model call. One phase, the change
/// set applied at once, a green verification verdict, a pull request; no provider key,
/// no token cost, repeatable as often as anyone likes.
/// <para>
/// What it proves: the WIRING. The production ChatClientFactory resolves the worker
/// bridge, the request carries the real tool surface, the worker's tool calls execute
/// against the real hosts, and the keystone + PR run untouched. What it deliberately does
/// NOT prove: BEHAVIOUR. A scripted worker always answers correctly — whether a live agent
/// CLI can actually drive this loop to green is what the operator's local run answers,
/// with the same bridge and one registration swapped.
/// </para>
/// </summary>
[Trait("Category", "PipelineHarness")]
public sealed class ExternalWorkerTicketTests
{
    private const string ScopeEstimateReply =
        """{"repos":[{"name":"csharp-fixture","affected":true,"confidence":0.95}],"complexity":"small","shape":"deterministic","shape_reason":"one guard clause, checked by building and testing"}""";

    private const string GreenVerdict =
        """Done. {"status":"green","build_ran":true,"build_passed":true,"tests_ran":true,"tests_passed":true,"summary":"fixed","acceptance":[{"criterion":"criterion 1","status":"met","evidence":"handled in the change"},{"criterion":"criterion 2","status":"met","evidence":"existing behaviour preserved"}]}""";

    private static ScriptedWorkerProcessRunner MechanicalTicketWorker() =>
        new ScriptedWorkerProcessRunner()
            // p0413a: ScopeRepos estimates every ticketed run — one repository has
            // nothing to scope, but its size and shape are still asked for, and in
            // worker mode the worker answers that call like every other.
            .EnqueueText(ScopeEstimateReply)
            .EnqueueText(SpecDerivationFixture.DerivationJson)
            .EnqueueToolCall("write_file", """{"path":"primary/src/Patch.cs","content":"// real fix"}""")
            .EnqueueToolCall("run_command", """{"command":"dotnet build","repo":"primary"}""")
            .EnqueueToolCall("update_progress",
                """{"items":[{"id":"guard","activity":"Answer an empty request body with 400","status":"done"}]}""")
            .EnqueueText(GreenVerdict);

    [Fact]
    public async Task Harness_MechanicalTicket_DrivenExternally_EndsGreen()
    {
        var worker = MechanicalTicketWorker();
        await using var harness = RealCompositionHarness.Build(
            FixturePaths.For(FixturePaths.Default),
            services =>
            {
                HarnessProjectAnalyzerStub.Register(services);
                ExternalWorkerHarness.DrivenBy(worker)(services);
            });

        var runner = new PipelineRunner(harness.Services) { AgentOverride = ExternalWorkerHarness.Agent() };
        var result = await runner.RunAsync("fix-bug");

        result.IsSuccess.Should().BeTrue(
            $"an externally driven ticket must pass the same keystone as a provider-driven one: {result.Message}");

        // The change reached the sandbox as a real write — the worker's tool call was
        // executed by the framework, not merely recorded.
        harness.StubSandboxFactory!.Spawned
            .SelectMany(s => s.Sandbox.RanSteps)
            .Should().Contain(s => s.Kind == AgentSmith.Sandbox.Wire.StepKind.WriteFile
                && s.Path != null && s.Path.EndsWith("src/Patch.cs", StringComparison.Ordinal));

        // A pull request, opened by the ordinary keystone path.
        runner.LastContext!.TryGet<List<OpenedPullRequest>>(ContextKeys.OpenedPullRequests, out var prs)
            .Should().BeTrue("an externally driven run ends in a PR like any other");
        prs!.Should().NotBeEmpty();

        harness.ChatClient.InvocationCount.Should().Be(0,
            "not one provider call may happen — the whole run is answered by the worker");
    }

    [Fact]
    public async Task Harness_ExternalWorker_SeesWhatTheProviderWouldSee()
    {
        var worker = MechanicalTicketWorker();
        await using var harness = RealCompositionHarness.Build(
            FixturePaths.For(FixturePaths.Default),
            services =>
            {
                HarnessProjectAnalyzerStub.Register(services);
                ExternalWorkerHarness.DrivenBy(worker)(services);
            });

        var runner = new PipelineRunner(harness.Services) { AgentOverride = ExternalWorkerHarness.Agent() };
        // Production opens the ambient run scope in ExecutePipelineUseCase; the harness
        // calls the executor directly, so the scope is opened here to assert on the
        // identity a deployed run would carry.
        using (harness.Services.GetRequiredService<IRunContextAccessor>().BeginScope("run-external"))
            await runner.RunAsync("fix-bug");

        worker.Prompts.Should().NotBeEmpty("the worker answered the run's model calls");
        var masterPrompt = worker.Prompts.FirstOrDefault(p => p.Contains("write_file", StringComparison.Ordinal));
        masterPrompt.Should().NotBeNull("the master call must offer the real tool surface");
        masterPrompt!.Should().Contain("input_schema",
            "tool schemas are what make the worker's answer executable");
        masterPrompt.Should().Contain("run_command").And.Contain("update_progress",
            "the worker is offered the same tools the provider would be");
        masterPrompt.Should().Contain("\"role\": \"system\"",
            "the system prompt is part of what a provider receives, so it is part of the request");
        masterPrompt.Should().Contain("run_id").And.Contain("step_index",
            "every request says which run and which step it belongs to");
    }
}
