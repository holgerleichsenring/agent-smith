using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Specs;
using AgentSmith.PipelineHarness.Composition;
using AgentSmith.Sandbox.Wire;
using AgentSmith.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;

namespace AgentSmith.PipelineHarness.Presets;

/// <summary>
/// 2026-10-13-50c7 through the REAL composition: an API project whose existing baseline owned only
/// its workdir is re-initialised; the context now declares its sibling library in sources, so the
/// library's oversized method is baselined, and a later spec touching the library passes the gate.
/// </summary>
[Trait("Category", "PipelineHarness")]
public sealed class ContextSourcesHarnessTests
{
    private const string Meta = ".agentsmith/contexts/default";
    private const string BaselinePath = Meta + "/structure-baseline.tsv";
    private const string Principles = "## Limits\n\n```yaml\nfunction_lines: 20\n```\n";
    private const string LibraryPath = "src/Inventory.Core/Repository.cs";

    // Written before 50c7: it records its Limits, but not the directories it owned.
    private const string OldBaseline =
        "# path\tkey\tmetric\tvalue — units over their Limits when measured, each tolerated up to its value\n"
        + "# measured with: function_lines=20 tolerance_percent=0\n";

    private const string ContextYaml = """
        meta:
          workdir: src/Inventory.Api
        stack:
          lang: csharp
          image: mcr.microsoft.com/dotnet/sdk:8.0
        sources:
          - src/Inventory.Core
        """;

    private const string GreenVerdict =
        """Done. {"status":"green","build_ran":true,"build_passed":true,"tests_ran":true,"tests_passed":true,"summary":"fixed","acceptance":[{"criterion":"criterion 1","status":"met","evidence":"handled"},{"criterion":"criterion 2","status":"met","evidence":"preserved"}]}""";

    // A method of n statements measures n + 3 lines.
    private static string Library(bool touched) =>
        "class Repository\n{\n    void Load()\n    {\n"
        + string.Concat(Enumerable.Range(0, 30).Select(i => $"        Step{i}();\n")) + "    }\n"
        + (touched ? "    void Save()\n    {\n        Flush();\n    }\n" : string.Empty) + "}\n";

    [Fact]
    public async Task Reinit_LibraryDeclaredInSources_BaselinesItAndALaterSpecTouchingItPasses()
    {
        var baseline = await ReinitAsync();

        baseline.Should().Contain($"{LibraryPath}\tRepository.Load()\tfunction_lines\t33")
            .And.Contain("# measured with: function_lines=20 tolerance_percent=0")
            .And.Contain("# owned:\tsrc/Inventory.Api\tsrc/Inventory.Core");
        var (writes, state, _) = await LaterSpecAsync(baseline, corrected: false);
        writes.Should().Be(1, "the baselined method did not grow, so no correction pass follows");
        state.Should().Be(SpecRunState.Done);
    }

    [Fact]
    public async Task LaterSpec_LibraryInSourcesButNotBaselined_IsSentBack()
    {
        // The contrast: the gate does judge the library through sources — without the entry the
        // re-init added, the same edit gets the correction pass.
        var (writes, state, prompts) = await LaterSpecAsync(OldBaseline, corrected: true);

        writes.Should().Be(2, "one correction pass");
        prompts.Should().Contain(p =>
            p.Contains($"{LibraryPath} Repository.Load(): function_lines 33 > 20", StringComparison.Ordinal));
        state.Should().Be(SpecRunState.Failed, "the correction kept the method as it was");
    }

    private static async Task<string> ReinitAsync()
    {
        await using var harness = RealCompositionHarness.Build(
            FixturePaths.For(FixturePaths.Default), SandboxBackend.Stub, session: null, SkillsBackend.Fixture,
            services => { HarnessProjectAnalyzerStub.Register(services); LimitsSkillsCatalogPath.Register(services); });
        var sandboxes = harness.StubSandboxFactory!;
        sandboxes.SourceFiles = new(StringComparer.Ordinal)
        {
            ["src/Inventory.Api/Endpoints.cs"] = "class Endpoints\n{\n    void Map() { }\n}\n",
            [LibraryPath] = Library(touched: false),
            [$"{Meta}/context.yaml"] = "meta:\n  workdir: src/Inventory.Api\nstack:\n  lang: csharp\n",
            [$"{Meta}/principles.md"] = Principles,
            [BaselinePath] = OldBaseline,
        };
        harness.ChatClient
            .EnqueueText(
                """
                { "status": "complete", "components": [
                    { "name": "default", "workdir": "src/Inventory.Api", "language": "csharp", "evidence": "Inventory.Api.csproj" } ] }
                """)
            .EnqueueToolCall("write_context_yaml",
                """{"repo":"","context_name":"default","document":{"meta":{"workdir":"src/Inventory.Api"},"stack":{"lang":"csharp","image":"mcr.microsoft.com/dotnet/sdk:8.0"},"sources":["src/Inventory.Core"]}}""")
            .EnqueueText("Bootstrap facts written.");

        var result = await new PipelineRunner(harness.Services).RunAsync("init-project");

        result.IsSuccess.Should().BeTrue(result.Message);
        return sandboxes.Spawned.SelectMany(s => s.Sandbox.RanSteps)
            .Where(s => s.Kind == StepKind.WriteFile && s.Path!.EndsWith(BaselinePath, StringComparison.Ordinal))
            .Should().ContainSingle().Subject.Content!;
    }

    private static async Task<(int Writes, SpecRunState State, IReadOnlyList<string> Prompts)> LaterSpecAsync(
        string baseline, bool corrected)
    {
        await using var harness = RealCompositionHarness.Build(
            FixturePaths.For(FixturePaths.Default), HarnessProjectAnalyzerStub.Register);
        ((StubSourceProviderFactory)harness.Services.GetRequiredService<ISourceProviderFactory>()).ContextYaml = ContextYaml;
        harness.StubSandboxFactory!.BaseFiles = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [$"{Meta}/principles.md"] = Principles,
            [BaselinePath] = baseline,
        };
        var write = $$"""{"path":"primary/{{LibraryPath}}","content":{{System.Text.Json.JsonSerializer.Serialize(Library(touched: true))}}}""";
        harness.ChatClient
            .EnqueueText(SpecDerivationFixture.DerivationJson)
            .EnqueueText("Planning: I will add Save to the repository.")
            .EnqueueToolCall("write_file", write)
            .EnqueueText(GreenVerdict);
        if (corrected)
            harness.ChatClient
                .EnqueueText("Correcting: I keep it as it is.")
                .EnqueueToolCall("write_file", write)
                .EnqueueText(GreenVerdict);

        var runner = new PipelineRunner(harness.Services);
        await runner.RunAsync("code");

        var state = runner.LastContext!.Get<SpecSequenceProgress>(ContextKeys.SpecSequenceProgress).Specs.Single().State;
        return (harness.ChatClient.ToolCalls.Count(c => c.Name == "write_file"), state, [.. harness.ChatClient.PromptsSeen]);
    }
}
