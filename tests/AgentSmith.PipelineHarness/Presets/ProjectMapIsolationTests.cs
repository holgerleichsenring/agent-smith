using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Models;
using AgentSmith.PipelineHarness.Composition;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;

namespace AgentSmith.PipelineHarness.Presets;

/// <summary>
/// 2026-09-09-6a77: the fast tier used to resolve the production DiskProjectMapStore,
/// so every preset run wrote its ProjectMap into the MACHINE's cache root under the
/// fixture's context name ("default", "primary", "secondary") — and every preset builds
/// over the same fixture with the same StubSandbox HEAD, so they all computed one content
/// hash and hit each other's entries. The presets do not agree on an analyzer: some stub
/// it, some leave the production ProjectAnalyzer in place and script it a bare "{}", which
/// parses into a ProjectMap with a null language and a null ci pair. Whichever missed
/// first decided the verify commands for every test after it, and the stub is only ever
/// consulted on a miss — which is how run 34345964136 failed eight keystone assertions on
/// a tree that passed on main minutes later.
/// </summary>
[Trait("Category", "PipelineHarness")]
public sealed class ProjectMapIsolationTests
{
    [Fact]
    public async Task MapCache_ASecondHarnessOverTheSameFixture_ReadsItsOwnAnalyzersMap()
    {
        // Harness 1 leaves the production ProjectAnalyzer in place and scripts it "{}" —
        // a parseable ProjectMap carrying no language and no build/test command.
        await using (var poisoner = RealCompositionHarness.Build(FixturePaths.For(FixturePaths.Default)))
        {
            poisoner.ChatClient
                .EnqueueText("{}")
                .EnqueueText(SpecDerivationFixture.DerivationJson)
                .EnqueueText("No changes needed.");
            await new PipelineRunner(poisoner.Services).RunAsync("fix-bug");
        }

        // Harness 2 stubs the analyzer with a csharp map carrying `dotnet build`. Its
        // keystone can only go green if the verify stage resolved ITS analyzer's commands.
        await using var harness = RealCompositionHarness.Build(
            FixturePaths.For(FixturePaths.Default), HarnessProjectAnalyzerStub.Register);
        harness.ChatClient
            .EnqueueText(SpecDerivationFixture.DerivationJson)
            .EnqueueToolCall("write_file", """{"path":"primary/src/Patch.cs","content":"// real fix"}""")
            .EnqueueToolCall("run_command", """{"command":"dotnet build","repo":"primary"}""")
            .EnqueueToolCall("update_progress", """{"items":[{"id":"guard","activity":"Answer an empty request body with 400","status":"done"}]}""")
            .EnqueueText("""Done. {"status":"green","build_ran":true,"build_passed":true,"tests_ran":true,"tests_passed":true,"summary":"fixed","acceptance":[{"criterion":"criterion 1","status":"met","evidence":"handled in the change"},{"criterion":"criterion 2","status":"met","evidence":"existing behaviour preserved"}]}""");

        var result = await new PipelineRunner(harness.Services).RunAsync("fix-bug");

        result.IsSuccess.Should().BeTrue(
            $"the second harness must verify against its OWN analyzer's map, not the first "
            + $"harness's: {result.Message}");
    }

    [Fact]
    public async Task MapStore_TwoHarnesses_ShareNothing()
    {
        var map = new ProjectMap(
            PrimaryLanguage: "csharp",
            Frameworks: [],
            Modules: [],
            TestProjects: [],
            EntryPoints: [],
            Conventions: new Conventions(null, null, null),
            Ci: new CiConfig(HasCi: false, BuildCommand: "dotnet build", TestCommand: null, CiSystem: null));

        await using var first = RealCompositionHarness.Build(FixturePaths.For(FixturePaths.Default));
        await using var second = RealCompositionHarness.Build(FixturePaths.For(FixturePaths.Default));

        await first.Services.GetRequiredService<IProjectMapStore>()
            .SetAsync("default", "hash", map, CancellationToken.None);
        var seen = await second.Services.GetRequiredService<IProjectMapStore>()
            .TryGetAsync("default", "hash", CancellationToken.None);

        seen.Should().BeNull("one harness's analysis must never reach another harness");
    }
}
