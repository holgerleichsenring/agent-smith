using AgentSmith.Application.Services.Sandbox;
using FluentAssertions;

namespace AgentSmith.Tests.Sandbox;

/// <summary>
/// A conflict is one path with two DIFFERENT post-images; the same bytes in two clones are
/// no conflict, and a path several senders carry is carried by the first only.
/// </summary>
public sealed class SandboxTreeConflictsTests
{
    private const string Blob1 = "100644 1111111111111111111111111111111111111111";
    private const string Blob2 = "100644 2222222222222222222222222222222222222222";

    [Fact]
    public void SandboxTreeConflicts_PathInTwoKeys_IsReportedOnce_WithBothKeys()
    {
        var conflicts = SandboxTreeConflicts.Of(
        [
            ("shop-dotnet", Own(("src/A.cs", Blob1), ("web/b.ts", Blob1))),
            ("shop-node", Own(("src/A.cs", Blob2), ("web/b.ts", "deleted"))),
        ]);

        conflicts.Conflicts.Select(c => c.Path).Should().Equal("src/A.cs", "web/b.ts");
        conflicts.Conflicts[0].Keys.Should().Equal("shop-dotnet", "shop-node");
        conflicts.Describe().Should().Be(
            "src/A.cs was changed in both shop-dotnet and shop-node; "
            + "web/b.ts was changed in both shop-dotnet and shop-node");
    }

    [Fact]
    public void SandboxTreeConflicts_DisjointPaths_IsEmpty()
    {
        var conflicts = SandboxTreeConflicts.Of(
        [
            ("shop-dotnet", Own(("src/A.cs", Blob1))),
            ("shop-node", Own(("web/b.ts", Blob2))),
        ]);

        conflicts.Conflicts.Should().BeEmpty();
        conflicts.Describe().Should().BeNull();
        conflicts.Excluded("shop-dotnet", "shop-node").Should().Equal("src/A.cs");
        conflicts.Excluded("shop-node", "shop-dotnet").Should().Equal("web/b.ts");
    }

    [Fact]
    public void SandboxTreeConflicts_SamePathSameBlob_IsNoConflict()
    {
        var conflicts = SandboxTreeConflicts.Of(
        [
            ("shop-dotnet", Own(("src/A.cs", Blob1))),
            ("shop-node", Own(("src/A.cs", Blob1))),
            ("shop-python", Own(("tools/c.py", Blob2))),
        ]);

        conflicts.Conflicts.Should().BeEmpty("two clones holding the same bytes disagree about nothing");
        conflicts.Excluded("shop-python", "shop-dotnet").Should().Equal("tools/c.py");
        conflicts.Excluded("shop-python", "shop-node").Should().Equal(["src/A.cs", "tools/c.py"],
            "the first sender carries a shared path, every later one leaves it out");
    }

    private static IReadOnlyDictionary<string, string> Own(params (string Path, string Image)[] changes) =>
        changes.ToDictionary(c => c.Path, c => c.Image, StringComparer.Ordinal);
}
