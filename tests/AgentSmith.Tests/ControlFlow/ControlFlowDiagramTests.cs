using AgentSmith.Contracts.Commands;
using FluentAssertions;

namespace AgentSmith.Tests.ControlFlow;

/// <summary>
/// p0408: the diagram is only trustworthy because this test exists. It regenerates the
/// committed SVG from the presets and fails on any difference — so a preset edit that
/// nobody redrew is a red build, not a slide that quietly stops being true.
/// </summary>
public sealed class ControlFlowDiagramTests
{
    private const string WriteFlag = "AGENTSMITH_WRITE_DIAGRAM";

    [Fact]
    public void ControlFlowDiagram_MatchesThePresets()
    {
        var path = Path.Combine(RepoRoot(), ControlFlowDiagram.ArtifactPath);
        var generated = ControlFlowDiagram.Render();

        if (Environment.GetEnvironmentVariable(WriteFlag) == "1")
        {
            File.WriteAllText(path, generated);
            return;
        }

        File.Exists(path).Should().BeTrue($"{ControlFlowDiagram.ArtifactPath} is a committed artifact");
        Normalize(File.ReadAllText(path)).Should().Be(Normalize(generated),
            $"the control-flow diagram is generated from the presets. Regenerate it: "
            + $"{WriteFlag}=1 dotnet test tests/AgentSmith.Tests --filter ControlFlowDiagram");
    }

    [Fact]
    public void ControlFlowDiagram_DrawsThePhaseBoundary()
    {
        var code = ControlFlowFacts.Of(PipelinePresets.CodeName);

        code.Steps.Where(s => s.InPhaseBlock).Select(s => s.Command)
            .Should().Equal(PipelinePresets.CodePhaseBlock,
                "the block the run splices per phase is the block the diagram encloses");
    }

    [Fact]
    public void ControlFlowDiagram_NamesTheMasterEachPipelineLoads()
    {
        foreach (var flow in ControlFlowFacts.All())
        {
            flow.Master.Should().NotBeEmpty();
            flow.Steps.Where(s => s.Model.Use == ModelUse.Loop && s.Model.Actor.Length == 0)
                .Should().OnlyContain(s => flow.ActorOf(s) == flow.Master,
                    "a master-loop step with no actor of its own runs the pipeline's master");
        }
    }

    private static string Normalize(string svg) =>
        svg.Replace("\r\n", "\n", StringComparison.Ordinal);

    private static string RepoRoot()
    {
        var dir = AppContext.BaseDirectory;
        for (var i = 0; i < 10; i++)
        {
            if (Directory.Exists(Path.Combine(dir, "website", "src", "static"))) return dir;
            dir = Directory.GetParent(dir)?.FullName ?? dir;
        }

        throw new InvalidOperationException($"Could not locate the repo root from '{AppContext.BaseDirectory}'");
    }
}
