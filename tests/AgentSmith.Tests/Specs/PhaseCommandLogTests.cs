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
        line.Length.Should().BeLessThan(PhaseCommandBudget.TailChars + 200);
    }

    /// <summary>
    /// p0470: p0452 capped the list at forty entries and dropped the oldest, which is the
    /// negation of this phase — the searches that prove an absence run early. A phase that
    /// ran more than the budget holds still hands over every command LINE; what gives way
    /// is output.
    /// </summary>
    [Fact]
    public void PhaseCommandLog_MoreCommandsThanBudget_KeepsEveryCommandLine()
    {
        var log = new PhaseCommandLog();
        for (var i = 0; i < 200; i++)
            log.Record("api", $"grep -RIn 'Legacy' --include='*.cs' src/module-{i}", Output(i));

        var evidence = log.Evidence();
        evidence.Should().HaveCount(201, "200 commands and the notice that leads them");
        for (var i = 0; i < 200; i++)
            evidence.Should().Contain(l => l.Contains($"src/module-{i}'", StringComparison.Ordinal));
    }

    /// <summary>
    /// Existence outlives output: dropping an entry destroys the fact the account needs most,
    /// that the command ran at all. Dropping its tail costs far less, so the tail goes first
    /// and the oldest tail goes before a newer one.
    /// </summary>
    [Fact]
    public void PhaseCommandLog_UnderPressure_ShortensOlderOutputBeforeNewer()
    {
        var log = new PhaseCommandLog();
        for (var i = 0; i < 200; i++)
            log.Record("api", $"grep -RIn 'Legacy' src/module-{i}", Output(i));

        var evidence = log.Evidence();
        evidence[1].Should().Contain("output not shown", "the oldest output pays first");
        evidence[^1].Should().Contain("output: ").And.Contain("matches in module-199",
            "the newest command still carries what it found");
    }

    /// <summary>A notice on a complete list would teach the reader to discount a complete
    /// one, so a record that lost nothing says nothing.</summary>
    [Fact]
    public void PhaseCommandLog_NothingLost_LeadsWithNoNotice()
    {
        var log = new PhaseCommandLog();
        for (var i = 0; i < 5; i++) log.Record("api", $"grep -RIn 'Legacy' src/module-{i}", Output(i));

        var evidence = log.Evidence();
        evidence.Should().HaveCount(5);
        evidence.Should().NotContain(l => l.StartsWith("not every command", StringComparison.Ordinal));
    }

    /// <summary>
    /// The silence IS the defect: a trimmed list and a complete one read identically, so an
    /// absent command is indistinguishable from one that never ran. The account may judge a
    /// partial record; it may not do so without knowing the record is partial.
    /// </summary>
    [Fact]
    public void PhaseCommandLog_SomethingLost_LeadsWithRanAndShownCounts()
    {
        var log = new PhaseCommandLog();
        for (var i = 0; i < 200; i++) log.Record("api", $"grep -RIn 'Legacy' src/module-{i}", Output(i));

        var evidence = log.Evidence();
        evidence[0].Should()
            .StartWith("not every command", "the notice leads the evidence, it is not buried in it")
            .And.Contain("ran 200 commands")
            .And.Contain("200 are listed")
            .And.Contain("A command that is missing here was still run.");
    }

    /// <summary>
    /// The counter is monotonic and lives beside the list, never derived from it: a count
    /// taken from the list it describes would degrade along with it, and the notice's whole
    /// job is to say the list degraded.
    /// </summary>
    [Fact]
    public void PhaseCommandLog_RanCount_SurvivesTheTrimmingItDescribes()
    {
        var log = new PhaseCommandLog();
        for (var i = 0; i < 400; i++) log.Record("api", new string('x', 300) + $" module-{i}", Output(i));

        log.Ran.Should().Be(400);
        log.Entries.Count.Should().BeLessThan(400, "commands this long exhaust the budget on their own");
        log.Evidence()[0].Should().Contain("ran 400 commands")
            .And.Contain($"{log.Entries.Count} are listed");
    }

    /// <summary>
    /// p0452 pins "no output" as the proof a search found nothing — the one reading that
    /// closes an absence criterion. A trimmed tail reusing that wording would claim the
    /// command found nothing, which is this phase's defect one level down.
    /// </summary>
    [Fact]
    public void PhaseCommandLog_TrimmedTail_ReadsDifferentlyFromNoOutput()
    {
        var log = new PhaseCommandLog();
        for (var i = 0; i < 200; i++) log.Record("api", $"grep -RIn 'Legacy' src/module-{i}", Output(i));
        log.Record("api", "grep -RIn 'Legacy' src/module-final", "");

        var evidence = log.Evidence();
        evidence[1].Should().Contain("output not shown")
            .And.NotContain("no output", "an output that was taken is not an output never produced");
        evidence[^1].Should().Contain("module-final").And.Contain("no output");
    }

    /// <summary>run_command takes arbitrary shell, and a heredoc patch is tens of kilobytes
    /// in one command. The retired entry count bounded that by accident.</summary>
    [Fact]
    public void PhaseCommandLog_AHeredocSizedCommand_IsStoredCapped()
    {
        var log = new PhaseCommandLog();
        log.Record("api", "cat > src/Sample.cs <<'EOF'\n" + new string('y', 40_000) + "\nEOF", "ok");

        log.Entries.Single().Command.Length.Should().BeLessThanOrEqualTo(PhaseCommandBudget.CommandChars + 1);
        log.Evidence().Single().Should().Contain("cat > src/Sample.cs");
    }

    /// <summary>
    /// The observed case: a live migration phase issued 157 run_command calls, and the search
    /// that proves an absence is one of the first the agent runs. Built from short synthetic
    /// strings this test would pass at any budget, so the sizes here are the real ones.
    /// </summary>
    [Fact]
    public void PhaseCommandLog_RealisticPhase_TheEarlySearchIsStillPresent()
    {
        var log = new PhaseCommandLog();
        log.Record("worker", "grep -RIn 'LegacyAuth' --include='*.cs' src/backend",
            "exit_code: 1\nelapsed_ms: 240\n\nstdout:\n\nstderr:");
        for (var i = 0; i < 156; i++)
            log.Record("worker", $"dotnet build src/backend/Sample.Module{i}/Sample.Module{i}.csproj", Output(i));

        var evidence = log.Evidence();
        evidence.Should().HaveCount(158, "157 commands and the notice");
        evidence[1].Should().Contain("grep -RIn 'LegacyAuth' --include='*.cs' src/backend")
            .And.Contain("exited 1", "the search that proves an absence exits non-zero");
        evidence.Sum(l => l.Length).Should().BeLessThan(PhaseCommandBudget.MaxChars + 500,
            "the point of a character budget is that it binds");
    }

    /// <summary>Record trims under the same lock the reader takes, so a phase recording while
    /// the account is being assembled cannot hand over a half-trimmed list.</summary>
    [Fact]
    public async Task PhaseCommandLog_ConcurrentRecordAndEvidence_DoNotTear()
    {
        var log = new PhaseCommandLog();
        var reads = new List<int>();

        var writing = Task.Run(() =>
        {
            for (var i = 0; i < 500; i++) log.Record("api", $"grep -RIn 'Legacy' src/module-{i}", Output(i));
        });
        var reading = Task.Run(() =>
        {
            for (var i = 0; i < 500; i++) reads.Add(log.Evidence().Count);
        });
        await Task.WhenAll(writing, reading);

        log.Ran.Should().Be(500);
        reads.Should().OnlyContain(count => count >= 0);
        log.Evidence().Should().OnlyContain(line => line.Length > 0);
    }

    /// <summary>
    /// p0469: a verify-stage line carries "exited N" and an agent's line carried nothing, so
    /// the reader could not tell a search that ran and found nothing — which is how an
    /// absence is proved, and which exits non-zero — from one that never ran at all.
    /// </summary>
    [Fact]
    public void PhaseCommandLog_Evidence_CarriesTheExitStatus()
    {
        var log = new PhaseCommandLog();
        log.Record("api", "grep -rn 'Sample' src",
            "exit_code: 1\nelapsed_ms: 12\ntruncated: false\n\nstdout:\n\nstderr:");
        log.Record("api", "dotnet build", "exit_code: 0\nelapsed_ms: 900\n\nstdout:\nBuild succeeded.");

        var evidence = log.Evidence();
        evidence[0].Should().Contain("exited 1",
            "a search that found nothing exits non-zero, and that is the proof");
        evidence[1].Should().Contain("exited 0");
    }

    /// <summary>An output with no status header says so rather than implying success.</summary>
    [Fact]
    public void ACommandWithoutAStatusHeader_SaysTheStatusIsNotRecorded()
    {
        var log = new PhaseCommandLog();
        log.Record("api", "grep -rn 'Sample' src", "some output");

        log.Evidence().Single().Should().Contain("exit status not recorded");
    }

    /// <summary>A blank command never ran, so it is not counted: counting it would make the
    /// notice announce a loss against a record that lost nothing.</summary>
    [Fact]
    public void ABlankCommand_IsNotEvidence()
    {
        var log = new PhaseCommandLog();
        log.Record("api", "   ", "ok");

        log.Evidence().Should().BeEmpty();
        log.Ran.Should().Be(0);
    }

    // A realistic run_command result: the status header the account reads past, and 400
    // characters of tail ending in the line that carries the verdict.
    private static string Output(int i) =>
        $"exit_code: 0\nelapsed_ms: 310\n\nstdout:\n{new string('.', 380)}\n{i} matches in module-{i}";
}
