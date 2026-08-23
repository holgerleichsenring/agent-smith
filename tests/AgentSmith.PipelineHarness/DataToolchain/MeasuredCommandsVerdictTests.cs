using FluentAssertions;

namespace AgentSmith.PipelineHarness.DataToolchain;

/// <summary>
/// p0505: the verdict column is the table's only derived field, so it is the only
/// one an offline test may police — and it must be policed, because the successor
/// profile may declare nothing this table does not mark <c>declarable</c>.
/// </summary>
public sealed class MeasuredCommandsVerdictTests
{
    private readonly MeasuredCommandsSource _source = new();
    private readonly MeasuredCommandVerdict _verdict = new();

    [Fact]
    public void MeasuredCommands_EveryDeclarableCommand_IsGreenOnItsCleanShapeAndRedOnANamedDefect()
    {
        var table = _source.Load();

        foreach (var (shape, command) in table.Pairs)
        {
            var variants = table.Variants(shape, command);
            if (variants.Values.First().Verdict != MeasuredCommandVerdict.Declarable) continue;

            variants[MeasuredCommand.CleanVariant].ExitCode.Should().Be(0,
                $"'{command}' reds on the clean {shape} fixture, so it is a broken command, not a gate");
            variants.Values.Should().Contain(
                r => r.IsRed && r.Variant != MeasuredCommand.CleanVariant
                    && r.Variant != MeasuredCommand.SyntaxDefect,
                $"'{command}' is declarable on {shape} only if a named non-syntax defect turns it red");
        }
    }

    [Fact]
    public void MeasuredCommands_ACommandRedOnlyOnTheSyntaxDefect_IsRecordedAsALinterNotAGate()
    {
        var table = _source.Load();

        foreach (var (shape, command) in table.Pairs)
        {
            var variants = table.Variants(shape, command);
            var recomputed = _verdict.Classify(variants);
            variants.Values.Select(r => r.Verdict).Distinct().Should().ContainSingle(
                $"'{command}' on {shape} carries one verdict across its variants");
            variants.Values.First().Verdict.Should().Be(recomputed,
                $"'{command}' on {shape} is recorded as its exit codes classify it");
        }
    }
}
