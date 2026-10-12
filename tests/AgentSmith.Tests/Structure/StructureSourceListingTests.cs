using FluentAssertions;

namespace AgentSmith.Tests.Structure;

/// <summary>
/// 2026-10-13-2d0c: the sandbox captures stdout line by line up to the cap, so a -z listing — one
/// line — is never cut and is accepted at any length.
/// </summary>
public sealed class StructureSourceListingTests
{
    [Fact]
    public async Task StructureSourceListing_LongZeroSeparatedLine_IsAccepted()
    {
        var paths = Enumerable.Range(0, 40_000).Select(i => $"src/Api/Generated/File{i:D6}.cs").ToList();
        var repo = new RepoSandbox { Listing = string.Join('\0', paths) + "\0\n" };
        repo.Listing.Length.Should().BeGreaterThan(1_000_000, "the listing is past the capture cap");

        var (listed, refusal) = await StructureBaselineStubs.Listing()
            .ListAsync(repo, "src/Api", [".cs"], CancellationToken.None);

        refusal.Should().BeNull();
        listed.Should().HaveCount(paths.Count).And.EndWith(paths[^1]);
    }
}
