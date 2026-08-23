using FluentAssertions;

namespace AgentSmith.PipelineHarness.DataToolchain;

/// <summary>
/// p0505: the offline guard over the measured table. It runs with no dbt, no
/// databricks CLI, no daemon and no network — so it can police shape, coverage
/// and the fixture pin, and it deliberately cannot police the exit codes: nothing
/// in a <c>dotnet test</c> run can re-derive what <c>dbt parse</c> did. What it
/// cannot detect is stated in the phase's decisions and is caught only by
/// re-running tools/measure-data-toolchain.sh.
/// </summary>
public sealed class MeasuredCommandsTableTests
{
    private readonly MeasuredCommandsSource _source = new();

    [Fact]
    public void MeasuredCommands_EveryRow_CarriesShapeVariantCommandExitNetworkToolVersionAndImage()
    {
        var table = _source.Load();

        table.Rows.Should().NotBeEmpty("the table is the phase's deliverable");
        foreach (var row in table.Rows)
        {
            row.Shape.Should().NotBeNullOrWhiteSpace();
            row.Variant.Should().NotBeNullOrWhiteSpace();
            row.Command.Should().NotBeNullOrWhiteSpace();
            row.Network.Should().BeOneOf("yes", "no");
            row.Verdict.Should().BeOneOf(MeasuredCommandVerdict.All);
            row.ToolVersion.Should().NotBeNullOrWhiteSpace(
                $"'{row.Command}' is a fact about a pinned tool, not about a tool name");
            row.Image.Should().NotBeNullOrWhiteSpace(
                $"'{row.Command}' is a fact about the image it ran in");
            row.FirstLine.Should().NotBeNullOrWhiteSpace();
        }
    }

    [Fact]
    public void MeasuredCommands_AFixtureChangedSinceTheMeasurement_GoesRedOnTheRecordedHash()
    {
        var table = _source.Load();
        var hasher = new DataToolchainFixtureHash();

        table.FixtureHashes.Should().NotBeEmpty("the table is pinned to the trees that produced it");
        foreach (var shape in _source.FixtureShapes())
        {
            table.FixtureHashes.Should().ContainKey(shape);
            hasher.Compute(Path.Combine(_source.FixturesDirectory(), shape))
                .Should().Be(table.FixtureHashes[shape],
                    $"the '{shape}' fixture changed without a re-measurement — "
                    + "run tools/measure-data-toolchain.sh");
        }
    }

    [Fact]
    public void MeasuredCommands_ShapeCoverage_MatchesTheFixtureDirectoriesBothWays()
    {
        var table = _source.Load();
        var shapes = _source.FixtureShapes().ToList();

        table.Shapes.Should().BeEquivalentTo(shapes,
            "a shape without rows is unmeasured, and a shape without a directory is unpinned");
        foreach (var shape in shapes)
        {
            var measured = table.Rows.Where(r => r.Shape == shape).Select(r => r.Variant).Distinct();
            measured.Should().BeEquivalentTo(_source.FixtureVariants(shape),
                $"every '{shape}' variant is measured and every measured variant exists");
        }
    }
}
