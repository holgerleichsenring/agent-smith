using FluentAssertions;

namespace AgentSmith.Tests.Architecture;

public sealed class HardCodedLimitScannerTests
{
    [Theory]
    [InlineData("    private const int MaxEntries = 20;")]
    [InlineData("    public static readonly TimeSpan Interval = TimeSpan.FromSeconds(30);")]
    [InlineData("        await Task." + "Delay(TimeSpan.FromMinutes(5), ct);")] // split: the wall-clock rule reads test sources
    [InlineData("        var top = items.Take(12).ToList();")]
    [InlineData("        cts.Cancel" + "After(5000);")]
    [InlineData("    private const int Bare = 5; // not a limit:")]
    public void Count_EachHardCodedBound_CountsOne(string line) =>
        HardCodedLimitScanner.Count(line).Should().Be(1);

    [Theory]
    [InlineData("    // private const int MaxEntries = 20;")]
    [InlineData("    /// a page holds .Take(20) rows")]
    [InlineData("    public int MaxSecondsPerSkillCall { get; set; } = 300;")]
    [InlineData("    public TimeSpan SendTimeout { get; init; } = TimeSpan.FromSeconds(2);")]
    [InlineData("    public const int Current = WireProtocol.Current;")]
    [InlineData("    public const int ExitIdle = 2; // not a limit: a process exit code")]
    public void Count_NoBoundOrAStatedNonLimit_CountsZero(string line) =>
        HardCodedLimitScanner.Count(line).Should().Be(0);

    [Fact]
    public void Count_AGetOnlyStaticProperty_Counts() =>
        HardCodedLimitScanner.Count("    static TimeSpan Wait { get; } = TimeSpan.FromSeconds(5);").Should().Be(1);

    [Fact]
    public void Count_ADeclarationSplitOverTwoLines_CountsOnce() =>
        HardCodedLimitScanner.Count("    private static readonly TimeSpan Wait =\n        TimeSpan.FromSeconds(5);").Should().Be(1);
}
