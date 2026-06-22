using AgentSmith.Application.Services.Handlers;
using FluentAssertions;

namespace AgentSmith.Tests.Handlers;

/// <summary>
/// p0279: the read-set match tolerates the format gap between repo/sandbox-prefixed
/// read paths and the free-form file a model emits — lenient by design (the risk to
/// avoid is a false downgrade of a legitimate analyzed_from_source finding).
/// </summary>
public sealed class ReadPathNormalizerTests
{
    [Theory]
    [InlineData("./src/A.cs", "src/A.cs")]
    [InlineData("src\\A.cs", "src/A.cs")]
    [InlineData("/src/A.cs", "src/A.cs")]
    public void Normalize_StripsLeadingDotSlashAndBackslashes(string input, string expected) =>
        ReadPathNormalizer.Normalize(input).Should().Be(expected);

    [Fact]
    public void ReadPathNormalizer_PrefixedRelativeAndBare_NormalizeAndMatch()
    {
        var readSet = new[] { "default/RHS.AuthPort.API/Program.cs" };

        // exact-relative variant, suffix variant, and bare-basename variant all match
        ReadPathNormalizer.WasRead(readSet, "default/RHS.AuthPort.API/Program.cs").Should().BeTrue();
        ReadPathNormalizer.WasRead(readSet, "RHS.AuthPort.API/Program.cs").Should().BeTrue("segment-suffix");
        ReadPathNormalizer.WasRead(readSet, "Program.cs").Should().BeTrue("basename");
        ReadPathNormalizer.WasRead(readSet, "./Program.cs").Should().BeTrue("normalized basename");
    }

    [Fact]
    public void WasRead_DifferentFileSameDir_DoesNotMatch()
    {
        var readSet = new[] { "default/api/Program.cs" };
        ReadPathNormalizer.WasRead(readSet, "default/api/Other.cs").Should().BeFalse();
        ReadPathNormalizer.WasRead(readSet, "Other.cs").Should().BeFalse();
    }

    [Fact]
    public void WasRead_EmptyOrNull_IsFalse()
    {
        ReadPathNormalizer.WasRead(null, "x").Should().BeFalse();
        ReadPathNormalizer.WasRead([], "x").Should().BeFalse();
        ReadPathNormalizer.WasRead(["a"], null).Should().BeFalse();
    }

    [Fact]
    public void WasRead_SuffixMatchIsSegmentAligned_NotSubstring()
    {
        // segment-suffix must align on a '/' boundary: "axb/Program.cs" is NOT a
        // segment-suffix of "b/Program.cs"; but the lenient basename fallback (same
        // file name) still preserves it — a deliberate lean against false downgrades.
        ReadPathNormalizer.WasRead(["axb/zeta.cs"], "b/omega.cs").Should().BeFalse("different basename, no segment-suffix");
        ReadPathNormalizer.WasRead(["axb/shared.cs"], "b/shared.cs").Should().BeTrue("basename fallback preserves");
    }
}
