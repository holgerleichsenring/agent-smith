using AgentSmith.Application.Services;
using FluentAssertions;

namespace AgentSmith.Tests.Services;

/// <summary>
/// 2026-08-28-cc40: the merge's answer to "are these two paths one file", now that a second
/// caller asks it. The read-set suppression has always depended on the suffix rule; the scan
/// scoreboard matches a delivered finding to a declared file with the same one, so a change
/// here moves both and both are covered by these cases.
/// </summary>
public sealed class MasterFindingsMergerPathTests
{
    [Theory]
    [InlineData("src/a.cs", "src/a.cs")]
    [InlineData("./src/a.cs", "src/a.cs")]
    [InlineData("src\\a.cs", "src/a.cs")]
    [InlineData("default/src/a.cs", "src/a.cs")]
    [InlineData("src/a.cs", "default/src/a.cs")]
    public void SamePath_TheSameFileUnderDifferentPrefixes_Matches(string left, string right) =>
        CitedPathMatch.Same(left, right).Should().BeTrue();

    [Theory]
    [InlineData("src/ba.cs", "src/a.cs")]
    [InlineData("src/a.cs", "src/b.cs")]
    [InlineData("other/a.cs", "src/a.cs")]
    [InlineData("", "src/a.cs")]
    [InlineData("src/a.cs", "")]
    public void SamePath_DifferentFiles_DoNotMatch(string left, string right) =>
        CitedPathMatch.Same(left, right).Should().BeFalse(
            "a suffix match on a segment boundary is what stops a.cs becoming ba.cs");
}
