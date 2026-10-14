using AgentSmith.Application.Services.Configuration;
using AgentSmith.Contracts.Models.Configuration;
using FluentAssertions;

namespace AgentSmith.Tests.Specs.Series;

/// <summary>2026-10-13-8762u: the series block — the series cap, the attempt size, and the measured spec block the
/// attempt is sized against the wall clock LEFT by, one block reserved for the commit-and-PR tail.</summary>
public sealed class SeriesConfigTests
{
    [Fact]
    public void SeriesConfig_Defaults_Are24And8AndUnmeasured()
    {
        var series = new SeriesConfig();

        (series.MaxSpecs, series.SpecsPerAttempt, series.SecondsPerSpec).Should().Be((24, 8, 0));
        series.Problems(1800).Should().BeEmpty();
    }

    [Fact]
    public void SeriesConfig_Problems_PerAttemptAboveStepCeiling_NamesTheField()
    {
        SeriesConfig.StepCeilingSpecs.Should().Be(18);

        new SeriesConfig { SpecsPerAttempt = 19 }.Problems().Should().ContainSingle()
            .Which.Should().Contain("series.specs_per_attempt is 19").And.Contain("between 1 and 18");
        new SeriesConfig { SpecsPerAttempt = 18 }.Problems().Should().BeEmpty();
    }

    [Fact]
    public void SeriesConfig_Problems_MaxSpecsAbove26_NamesTheField()
    {
        new SeriesConfig { MaxSpecs = 27 }.Problems().Should().ContainSingle()
            .Which.Should().Contain("series.max_specs is 27").And.Contain("a..z");
        new SeriesConfig { MaxSpecs = 0, SpecsPerAttempt = 1 }.Problems().Should().Contain(p => p.Contains("series.max_specs is 0"));
        new SeriesConfig { SecondsPerSpec = -1 }.Problems().Should().ContainSingle().Which.Should().Contain("series.seconds_per_spec is -1");
    }

    [Fact]
    public void SeriesConfig_Problems_PerAttemptAboveMaxSpecs_NamesTheField()
    {
        new SeriesConfig { MaxSpecs = 6, SpecsPerAttempt = 8 }.Problems().Should().ContainSingle()
            .Which.Should().Contain("series.specs_per_attempt is 8").And.Contain("between 1 and 6");
    }

    [Fact]
    public void SeriesConfig_Problems_TwoBlocksBeyondRunWallClock_NamesBothFields()
    {
        var series = new SeriesConfig { SecondsPerSpec = 1294 };

        series.Problems(1800).Should().ContainSingle().Which.Should()
            .Contain("series.seconds_per_spec is 1294").And.Contain("orchestrator.max_run_wall_time_seconds (1800)");
        series.Problems(2588).Should().BeEmpty("two blocks fit exactly");
        series.Problems(0).Should().BeEmpty("no wall clock is no ceiling");
        series.Problems().Should().BeEmpty("the cross-section fit is the run's, not the block's own");
    }

    [Fact]
    public void SeriesConfig_AttemptSize_IsTheSmallerOfConfiguredAndWhatFitsTheWallClock()
    {
        new SeriesConfig().AttemptSize(10).Should().Be(8, "unmeasured, the attempt is specs_per_attempt");
        new SeriesConfig { SecondsPerSpec = 1294 }.AttemptSize(9 * 1294).Should().Be(8);
        new SeriesConfig { SecondsPerSpec = 1294 }.AttemptSize(20 * 1294).Should().Be(8);
        new SeriesConfig { SecondsPerSpec = 1294 }.AttemptSize(5 * 1294 + 1293).Should().Be(4);
    }

    [Fact]
    public void SeriesConfig_AttemptSize_ReservesOneBlockForTheCommitAndPrTail()
    {
        var series = new SeriesConfig { SecondsPerSpec = 100 };

        series.AttemptSize(200).Should().Be(1, "one spec plus the tail block");
        series.AttemptSize(199).Should().Be(0, "a spec without its tail block does not fit");
        series.AttemptSize(-50).Should().BeNegative("never floored");
    }

    [Fact]
    public void SeriesConfigValidator_Problem_IsBlocking_Unmeasured_IsAdvisory()
    {
        var invalid = new AgentSmithConfig { Series = new SeriesConfig { MaxSpecs = 30 } };
        var measured = new AgentSmithConfig { Series = new SeriesConfig { SecondsPerSpec = 600 } };

        SeriesConfigValidator.Findings(invalid).Should().Contain(f => f.IsBlocking && f.Reason.Contains("series.max_specs is 30"))
            .And.Contain(f => !f.IsBlocking && f.Field == "series.seconds_per_spec");
        SeriesConfigValidator.Findings(measured).Should().BeEmpty();
        new AgentSmithConfigValidator(new SkillsConfigValidator()).Validate(new AgentSmithConfig())
            .Should().BeEmpty("an advisory never blocks the schema check");
    }
}
