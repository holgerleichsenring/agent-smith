using AgentSmith.Infrastructure.Core.Services.Configuration;
using FluentAssertions;
using Xunit;

namespace AgentSmith.Tests.ConfigStudio;

/// <summary>
/// p0353 diagnosis: proves whether orchestrator.max_run_wall_time_seconds survives
/// the full YAML-import -> decompose -> store -> assemble round-trip at HEAD. If this
/// passes, the code is correct and a running 30-min cancel is a stale deployed binary
/// (or a DB missing the orchestrator doc), NOT a code bug.
/// </summary>
public sealed class OrchestratorWallTimeRoundTripTests
{
    private const string Yaml = "orchestrator:\n  max_run_wall_time_seconds: 5400\n";

    [Fact]
    public void YamlBinding_MapsSnakeCaseWallTime()
    {
        var raw = RawConfigYaml.Deserialize(Yaml);
        raw.Orchestrator.MaxRunWallTimeSeconds.Should().Be(5400);
    }

    [Fact]
    public void ImportThenAssemble_PreservesWallTime()
    {
        using var h = new DbConfigTestHarness();
        h.Import(Yaml);

        var raw = h.Assembler.Assemble(h.DocStore.LoadAll());

        raw.Orchestrator.MaxRunWallTimeSeconds.Should().Be(5400);
    }
}
