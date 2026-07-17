using System.Reflection;
using AgentSmith.Contracts.Commands;
using FluentAssertions;

namespace AgentSmith.Tests.ContractsCoverage;

/// <summary>
/// p0344b: single-source-of-truth coverage for <see cref="CommandBeats"/> —
/// the deterministic command→beat mapping the server-side run-story derivation
/// reads. Mirrors <see cref="CommandDisplayNamesCoverageTests"/>: reflects over
/// every public const string on <see cref="CommandNames"/> so a new command
/// cannot silently fall out of the storybar, and pins that every code-defined
/// pipeline preset maps entirely through typed command names (never labels).
/// </summary>
public sealed class CommandBeatsCoverageTests
{
    // p0344b spec test: Beats_TypedCommands_MapDeterministically_PerPipeline
    [Fact]
    public void Beats_TypedCommands_MapDeterministically_PerPipeline()
    {
        foreach (var pipeline in PipelinePresets.Names)
        {
            var commands = PipelinePresets.TryResolve(pipeline)!;
            foreach (var command in commands)
                CommandBeats.TryGet(command, out _).Should().BeTrue(
                    $"preset '{pipeline}' command '{command}' must map to a run-story beat");
        }
    }

    [Fact]
    public void CommandBeatsCoverage_ReflectsCommandNamesConstants_NoCommandWithoutBeat()
    {
        var constants = ReflectCommandNameConstants();
        constants.Should().NotBeEmpty();

        var missing = constants
            .Where(c => !CommandBeats.All.ContainsKey(c.Value))
            .Select(c => $"{c.Name} = \"{c.Value}\"")
            .ToList();

        missing.Should().BeEmpty(
            "every public const string on CommandNames must have a beat in CommandBeats; missing: "
            + string.Join(", ", missing));
    }

    [Fact]
    public void CommandBeats_ParameterisedCommand_ResolvesViaBaseCommand()
    {
        CommandBeats.TryGet($"{CommandNames.SkillRound}:architect:1", out var beat).Should().BeTrue();
        beat.Should().Be(RunBeat.Building);
    }

    [Fact]
    public void CommandBeats_UnknownCommand_DoesNotResolve()
    {
        CommandBeats.TryGet("ThisCommandHasNoBeat_p0344b_test_fixture", out _).Should().BeFalse();
    }

    [Fact]
    public void CommandBeats_AnchorCommands_MapToTheirSpecifiedBeats()
    {
        // The spec-named anchors, pinned so a refactor cannot quietly reshuffle them.
        Get(CommandNames.FetchTicket).Should().Be(RunBeat.Ticket);
        Get(CommandNames.CheckoutSource).Should().Be(RunBeat.Ticket);
        Get(CommandNames.GeneratePlan).Should().Be(RunBeat.Plan);
        Get(CommandNames.Approval).Should().Be(RunBeat.Plan);
        Get(CommandNames.PlanOpenQuestions).Should().Be(RunBeat.Plan);
        Get(CommandNames.AgenticMaster).Should().Be(RunBeat.Building);
        Get(CommandNames.AnalyzeCode).Should().Be(RunBeat.Building);
        Get(CommandNames.AgenticExecute).Should().Be(RunBeat.Building);
        Get(CommandNames.RunVerifyPhase).Should().Be(RunBeat.Verify);
        Get(CommandNames.WriteRunResult).Should().Be(RunBeat.Outcome);
        Get(CommandNames.CommitAndPR).Should().Be(RunBeat.Outcome);
        Get(CommandNames.PrCrossLink).Should().Be(RunBeat.Outcome);
    }

    private static RunBeat Get(string command)
    {
        CommandBeats.TryGet(command, out var beat).Should().BeTrue($"'{command}' must have a beat");
        return beat;
    }

    private static IReadOnlyList<(string Name, string Value)> ReflectCommandNameConstants() =>
        typeof(CommandNames)
            .GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy)
            .Where(f => f.IsLiteral && !f.IsInitOnly && f.FieldType == typeof(string))
            .Select(f => (f.Name, (string)f.GetRawConstantValue()!))
            .ToList();
}
