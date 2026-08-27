using AgentSmith.Application.Services;
using AgentSmith.Application.Services.Tools;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Domain.Models;
using AgentSmith.Infrastructure.Core.Services.Skills;
using AgentSmith.Infrastructure.Services.Sandbox;
using AgentSmith.PipelineHarness.DataToolchain;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit.Abstractions;

namespace AgentSmith.PipelineHarness.Evals;

/// <summary>
/// p0505: what would the project analyzer have declared for a dbt repository?
/// Verify resolution reads declared ci commands FIRST and those come from the
/// analyzer fresh every run, so a profile's list is only ever reached if the
/// analyzer draws nothing. That premise was unproven; this measures it.
/// <para>
/// The phase-gate hook runs <c>dotnet test AgentSmith.sln</c> with NO filter, so this
/// test executes on every future phase commit on the operator's machine, where the
/// credentials live. It therefore asserts what ExpectationGoldenEvalTests asserts and
/// nothing more — the entry count and that the file exists. No assertion about what
/// the model said, ever: a content assertion here becomes a paid, flaky gate forever.
/// </para>
/// </summary>
[Trait("Category", "LiveLLM")]
public sealed class DataRepositoryAnalyzerEvalTests(ITestOutputHelper output)
{
    // One run does not settle a non-deterministic question.
    private const int Runs = 3;

    [Fact]
    public async Task AnalyzerEval_OverTheCleanDbtFixture_WritesThreeRunsAndAssertsNothingAboutTheModel()
    {
        var env = EvalChatClientEnv.TryBuild();
        if (env is null)
        {
            output.WriteLine("SKIP: no AZURE_OPENAI_API_KEY / OPENAI_API_KEY in env — "
                + "the eval tier is paid-API and opt-in.");
            return;
        }

        var (client, modelId) = env.Value;
        var repository = CopyOfCleanDbtFixture();
        var maps = new List<ProjectMap>();
        try
        {
            for (var run = 0; run < Runs; run++)
                maps.Add(await AnalyzeAsync(client, modelId, repository));
        }
        finally
        {
            Directory.Delete(repository, recursive: true);
        }

        var path = new AnalyzerCiCommandReport().Write(
            maps, modelId, new EmbeddedSkillsCatalog().Version, new CheckoutRoot().ReportsDirectory());
        output.WriteLine($"Report: {path}");
        maps.Should().HaveCount(Runs);
        File.Exists(path).Should().BeTrue();
    }

    private static Task<ProjectMap> AnalyzeAsync(
        Microsoft.Extensions.AI.IChatClient client, string modelId, string repository)
    {
        var analyzer = new ProjectAnalyzer(
            new ToolLoopChatFactory(client, modelId),
            new PackagedMasterPromptCatalog("project-analyzer-system", "project-analyzer-master"),
            new ProjectMapJsonReader(),
            new ProjectMapFinalizer(
                new ProjectMapJsonReader(), new EvalRunContext(),
                NullLogger<ProjectMapFinalizer>.Instance),
            new EvalRunContext(), new AgenticToolSurface(),
            NullLogger<ProjectAnalyzer>.Instance);
        var sandbox = new InProcessSandbox(
            "p0505-analyzer-eval", repository, ownsWorkDir: false, NullLogger.Instance);
        return analyzer.AnalyzeAsync(
            repository, new AgentConfig { Type = "openai" }, sandbox,
            CancellationToken.None, repoName: "sample-dbt");
    }

    // The analyzer runs against a COPY for the same reason every measured command
    // does: the harness re-copies Fixtures/** on every build, so anything written
    // into the source tree comes back as a rebuild artifact.
    private static string CopyOfCleanDbtFixture()
    {
        var source = Path.Combine(new CheckoutRoot().FixturesDirectory(), "dbt", "clean");
        var target = Path.Combine(Path.GetTempPath(), $"p0505-analyzer-{Guid.NewGuid():N}");
        foreach (var file in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories))
        {
            var destination = Path.Combine(target, Path.GetRelativePath(source, file));
            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
            File.Copy(file, destination);
        }
        return target;
    }
}
