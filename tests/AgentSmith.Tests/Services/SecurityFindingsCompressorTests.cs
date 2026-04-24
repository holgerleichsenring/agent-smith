using AgentSmith.Application.Services;
using FluentAssertions;

namespace AgentSmith.Tests.Services;

public sealed class SecurityFindingsCompressorTests
{
    private static Dictionary<string, string> SampleSlices() => new(StringComparer.OrdinalIgnoreCase)
    {
        ["secrets"] = "secrets slice content",
        ["injection"] = "injection slice content",
        ["config"] = "config slice content",
    };

    [Fact]
    public void GetSliceForSkill_EmptyInputCategories_ReturnsEmpty()
    {
        var result = SecurityFindingsCompressor.GetSliceForSkill(
            "chain-analyst", SampleSlices(), Array.Empty<string>());

        result.Should().BeEmpty();
    }

    [Fact]
    public void GetSliceForSkill_NullInputCategories_ReturnsEmpty()
    {
        var result = SecurityFindingsCompressor.GetSliceForSkill(
            "chain-analyst", SampleSlices(), null);

        result.Should().BeEmpty();
    }

    [Fact]
    public void GetSliceForSkill_WildcardInputCategories_ReturnsAllSlicesJoined()
    {
        var result = SecurityFindingsCompressor.GetSliceForSkill(
            "false-positive-filter", SampleSlices(), new[] { "*" });

        result.Should().Contain("secrets slice content");
        result.Should().Contain("injection slice content");
        result.Should().Contain("config slice content");
    }

    [Fact]
    public void GetSliceForSkill_ExplicitCategories_ReturnsOnlyMatching()
    {
        var result = SecurityFindingsCompressor.GetSliceForSkill(
            "auth-reviewer", SampleSlices(), new[] { "secrets", "injection" });

        result.Should().Contain("secrets slice content");
        result.Should().Contain("injection slice content");
        result.Should().NotContain("config slice content");
    }

    [Fact]
    public void GetSliceForSkill_UnknownCategory_Skipped()
    {
        var result = SecurityFindingsCompressor.GetSliceForSkill(
            "auth-reviewer", SampleSlices(), new[] { "nonexistent", "secrets" });

        result.Should().Contain("secrets slice content");
        result.Should().NotContain("nonexistent");
    }
}
