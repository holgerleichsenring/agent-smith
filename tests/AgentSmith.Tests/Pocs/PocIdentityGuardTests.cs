using AgentSmith.Application.Services.Pocs;
using AgentSmith.Contracts.Models.Configuration;
using FluentAssertions;

namespace AgentSmith.Tests.Pocs;

/// <summary>2026-11-23-933db: the different-app guard asks only when a share AND a count of files differ, or the port
/// or the entrypoint changed — never for a base image alone, never for a PoC with nothing validated.</summary>
public sealed class PocIdentityGuardTests
{
    private const string Cmd = "CMD [\"python\", \"app.py\"]";
    private static readonly PocDockerfile Same8000 = new(8000, Cmd, "python:3.12");
    private readonly PocArchiveLimits _limits = new();

    [Fact]
    public void Compare_NoValidatedRevision_AnswersSame()
    {
        PocIdentityGuard.Compare(null, null, null, Files(3), Same8000, _limits)
            .Should().Be(new PocMatch(3, 0, 0, 0, 0, false, false, PocMatchVerdicts.Same));
    }

    [Theory]
    [InlineData(134, 80, PocMatchVerdicts.Same)] // 60 % but only 80 files
    [InlineData(750, 300, PocMatchVerdicts.Same)] // 300 files but only 40 %
    [InlineData(500, 300, PocMatchVerdicts.Ask)] // 60 % and 300 files
    public void Compare_TheShareAndTheCount_MustBothBeOver(int files, int changed, string verdict)
    {
        var next = Files(files).Select((f, i) => i < changed ? f with { Crc32 = f.Crc32 + 1 } : f).ToList();

        var match = PocIdentityGuard.Compare(Manifest(files), 8000, Cmd, next, Same8000, _limits);

        (match.Changed, match.Unchanged, match.Added, match.Removed).Should().Be((changed, files - changed, 0, 0));
        match.RatioPercent.Should().Be(changed * 100 / files);
        match.Verdict.Should().Be(verdict);
    }

    [Fact]
    public void Compare_AddedAndRemovedFiles_CountAsDiffering()
    {
        var next = Files(300).Skip(200).Select(f => f with { Path = "new/" + f.Path }).ToList();

        var match = PocIdentityGuard.Compare(Manifest(300), 8000, Cmd, next, Same8000, _limits);

        (match.Added, match.Removed, match.Verdict).Should().Be((100, 300, PocMatchVerdicts.Ask));
    }

    [Fact]
    public void Compare_APortOrAnEntrypointChange_Asks_ABaseImageChangeAlone_DoesNot()
    {
        var port = PocIdentityGuard.Compare(Manifest(5), 8000, Cmd, Files(5), Same8000 with { Port = 3000 }, _limits);
        var entry = PocIdentityGuard.Compare(Manifest(5), 8000, Cmd, Files(5), Same8000 with { Entrypoint = "CMD node" }, _limits);
        var image = PocIdentityGuard.Compare(Manifest(5), 8000, Cmd, Files(5), Same8000 with { BaseImage = "python:3.13" }, _limits);

        (port.PortChanged, port.Verdict).Should().Be((true, PocMatchVerdicts.Ask));
        (entry.EntrypointChanged, entry.Verdict).Should().Be((true, PocMatchVerdicts.Ask));
        image.Should().Be(new PocMatch(0, 0, 0, 5, 0, false, false, PocMatchVerdicts.Same));
    }

    [Fact]
    public void Compare_AnUnchangedFile_IsEqualOnCrcAndSizeAgainstAStoredManifest()
    {
        var stored = PocManifest.FromJson(Manifest(2).ToJson());

        PocIdentityGuard.Compare(stored, 8000, Cmd, Files(2), Same8000, _limits).Unchanged.Should().Be(2);
    }

    private static List<PocArchiveEntry> Files(int count) =>
        [.. Enumerable.Range(0, count).Select(i => new PocArchiveEntry($"src/f{i}.py", (uint)(1000 + i), 10 + i))];

    // Its sha256 never matches a central-directory CRC; the guard compares CRC-32 and size only.
    private static PocManifest Manifest(int count) =>
        new(Files(count).ToDictionary(f => f.Path, f => new PocManifestEntry(new string('a', 64), f.Crc32, f.Size)));
}
