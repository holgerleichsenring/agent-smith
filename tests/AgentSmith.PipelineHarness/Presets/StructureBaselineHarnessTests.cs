using AgentSmith.Contracts.Providers;
using AgentSmith.PipelineHarness.Composition;
using AgentSmith.Sandbox.Wire;
using AgentSmith.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;

namespace AgentSmith.PipelineHarness.Presets;

/// <summary>
/// 2026-10-06-8565c through the REAL composition: an init whose C# delta declares Limits, over a
/// repository holding one oversized method, records that method as the baseline beside the
/// transferred principles.md and names the count in the init pull request.
/// </summary>
[Trait("Category", "PipelineHarness")]
public sealed class StructureBaselineHarnessTests
{
    private const string BaselinePath = ".agentsmith/contexts/default/structure-baseline.tsv";

    private static string LongMethod() =>
        "class Handler\n{\n    void Handle()\n    {\n"
        + string.Concat(Enumerable.Range(0, 30).Select(i => $"        Step{i}();\n")) + "    }\n}\n";

    [Fact]
    public async Task InitProject_OneOversizedMethod_WritesABaselineNamingItAndThePrSaysOneUnit()
    {
        await using var harness = RealCompositionHarness.Build(
            FixturePaths.For(FixturePaths.Default), SandboxBackend.Stub, session: null, SkillsBackend.Fixture,
            services => { HarnessProjectAnalyzerStub.Register(services); LimitsSkillsCatalogPath.Register(services); });
        var sandboxes = harness.StubSandboxFactory!;
        sandboxes.SourceFiles = new(StringComparer.Ordinal)
        {
            ["src/Handler.cs"] = LongMethod(),
            ["src/Short.cs"] = "class Short\n{\n    void Run() { }\n}\n",
        };
        sandboxes.MissingPaths.Add(BaselinePath);
        EnqueueTransferredInit(harness);

        var result = await new PipelineRunner(harness.Services).RunAsync("init-project");

        result.IsSuccess.Should().BeTrue(result.Message);
        var written = sandboxes.Spawned.SelectMany(s => s.Sandbox.RanSteps)
            .Where(s => s.Kind == StepKind.WriteFile && s.Path!.EndsWith(BaselinePath, StringComparison.Ordinal))
            .Should().ContainSingle().Subject.Content;
        written.Should().Contain("src/Handler.cs\tHandler.Handle()\tfunction_lines\t33").And.NotContain("Short");
        var factory = (StubSourceProviderFactory)harness.Services.GetRequiredService<ISourceProviderFactory>();
        factory.Created.Select(p => p.LastDescription).OfType<string>()
            .Should().Contain(body => body.Contains("1 unit(s) already exceed the Limits"));
    }

    // Discovery names one C# component; with the core transferred, the skill only writes facts.
    private static void EnqueueTransferredInit(RealCompositionHarness harness) =>
        harness.ChatClient
            .EnqueueText(
                """
                { "status": "complete", "components": [
                    { "name": "default", "workdir": ".", "language": "csharp", "evidence": "Fixture.csproj" } ] }
                """)
            .EnqueueToolCall("write_context_yaml",
                """{"repo":"","context_name":"default","document":{"meta":{"workdir":"."},"stack":{"lang":"csharp","image":"mcr.microsoft.com/dotnet/sdk:8.0"}}}""")
            .EnqueueText("Bootstrap facts written.");
}
