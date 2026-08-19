using AgentSmith.Application.Services.Specs;
using FluentAssertions;

namespace AgentSmith.Tests.Specs;

/// <summary>
/// p0452: the account could not see the agent's own commands, and refused three runs for it.
/// <para>
/// 459d "no listed command ran the required search". 587c "covers the Server repository
/// only". 929f "records a planned scan but provides no completed scan" — while the agent had
/// run nineteen searching commands across both repositories. The evidence existed and the
/// reader asked to judge it was never shown it.
/// </para>
/// </summary>
public sealed class PhaseCommandLogTests
{
    [Fact]
    public void ACommandTheAgentRan_IsEvidenceTheAccountCanRead()
    {
        var log = new PhaseCommandLog();
        log.Record("worker", "grep -RIn 'Legacy' --include='*.cs' .", "no matches");

        log.Evidence().Should().ContainSingle()
            .Which.Should().Contain("worker").And.Contain("grep -RIn").And.Contain("no matches");
    }

    /// <summary>
    /// A search that found nothing is the whole point — "no output" must read as a result,
    /// not as an absence of evidence.
    /// </summary>
    [Fact]
    public void ACommandThatPrintedNothing_StillSaysItRan()
    {
        var log = new PhaseCommandLog();
        log.Record("worker", "grep -RIn 'Legacy' .", "");

        log.Evidence().Single().Should().Contain("grep").And.Contain("no output");
    }

    /// <summary>
    /// The END of the output carries the verdict — a build's final error, a test run's
    /// summary line — so that is the half that survives the cap.
    /// </summary>
    [Fact]
    public void ALongOutput_KeepsItsEnd()
    {
        var log = new PhaseCommandLog();
        log.Record("api", "dotnet build", new string('x', 5_000) + " Build succeeded. 0 Error(s)");

        var line = log.Evidence().Single();
        line.Should().Contain("Build succeeded. 0 Error(s)");
        line.Length.Should().BeLessThan(PhaseCommandLog.TailChars + 200);
    }

    [Fact]
    public void APhaseThatRanHundredsOfCommands_HandsOverItsLatest()
    {
        var log = new PhaseCommandLog();
        for (var i = 0; i < PhaseCommandLog.MaxEntries + 25; i++) log.Record("api", $"step-{i}", "ok");

        var evidence = log.Evidence();
        evidence.Should().HaveCount(PhaseCommandLog.MaxEntries);
        evidence[^1].Should().Contain($"step-{PhaseCommandLog.MaxEntries + 24}");
        evidence.Should().NotContain(l => l.Contains("'step-0'", StringComparison.Ordinal));
    }

    [Fact]
    public void ABlankCommand_IsNotEvidence()
    {
        var log = new PhaseCommandLog();
        log.Record("api", "   ", "ok");

        log.Evidence().Should().BeEmpty();
    }
}
