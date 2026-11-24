using AgentSmith.Application.Services.Archives;
using AgentSmith.Server.Services.References;
using FluentAssertions;
using static AgentSmith.Tests.References.ReferenceUploadRequests;

namespace AgentSmith.Tests.References;

/// <summary>2026-10-01-283db: the one archive a set may arrive as is counted, not trusted.</summary>
public sealed class ReferenceZipReaderTests
{
    private readonly ReferenceZipReader _reader = new(new ReferencePathRule(), new ReferenceIgnoreList(), new BoundedZipInflater(new ZipEntryChecksum()));

    [Fact]
    public void ReferenceUpload_ZipEntryLyingAboutItsSize_StopsAtTheSetBound()
    {
        var bomb = Lying(Zip(("site/zeros.txt", new byte[ReferenceUploadLimits.MaxSetBytes + 1024])), claimed: 10);

        var read = _reader.Read("site.zip", bomb);

        // The runtime stops at the claimed ten bytes, so the entry arrives cut short — and is
        // refused by its checksum rather than stored as a ten-byte file nobody uploaded.
        read.IsRefused.Should().BeTrue();
        read.Refusal.Should().Contain("'site/zeros.txt'").And.Contain("checksum");
        read.Files.Should().BeEmpty();
    }

    [Fact]
    public void ReferenceZipReader_AnEntryDeclaringOverTheFileBound_IsLeftOutUninflated()
    {
        var read = _reader.Read("site.zip", Zip(("site/zeros.txt", new byte[ReferenceUploadLimits.MaxFileBytes + 1]),
            ("site/index.html", Text("<p>"))));

        read.Files.Select(f => f.Path).Should().Equal("site/index.html");
        read.LeftOut.Should().ContainSingle().Which.Path.Should().Be("site/zeros.txt");
    }

    [Fact]
    public void ReferenceZipReader_KeptEntriesOverTheSetBound_AreRefusedWhileInflating()
    {
        var parts = Enumerable.Range(0, 3).Select(i => ($"site/{i}.bin", new byte[10L * 1024 * 1024])).ToArray();

        _reader.Read("site.zip", Zip(parts)).Refusal.Should().Contain("'site/2.bin'").And.Contain("25 MB set limit");
    }

    [Fact]
    public void ReferenceZipReader_FilesWithNoSharedFolder_AreRootedUnderTheArchiveName()
    {
        var read = _reader.Read("landing.zip", Zip(("index.html", Text("<p>")), ("css/a.css", Text("a{}"))));

        read.Files.Select(f => f.Path).Should().Equal("landing/index.html", "landing/css/a.css");
    }

    [Fact]
    public void ReferenceZipReader_MacEntries_AreDroppedBeforeTheCount()
    {
        var read = _reader.Read("s.zip", Zip(("s/index.html", Text("<p>")), ("__MACOSX/s/._index.html", Text("x"))));

        read.Files.Select(f => f.Path).Should().Equal("s/index.html");
    }

    // 2026-10-02-075da: any authored entry is kept; a rebuildable one is named and never inflated.
    [Fact]
    public void ReferenceZipReader_RebuildableEntries_AreNotInflated()
    {
        var read = _reader.Read("s.zip", Zip(("s/app.py", Text("print()")), ("s/LICENSE", Text("MIT")),
            ("s/node_modules/x/i.js", Text("x"))));

        read.Files.Select(f => f.Path).Should().Equal("s/app.py", "s/LICENSE");
        read.LeftOut.Should().Equal(new AgentSmith.Server.Models.ReferenceLeftOut("s/node_modules/", "rebuildable"));
    }

    // 2026-11-23-933db: characterisation — the extracted BoundedZipInflater leaves the reader's answer unchanged.
    [Fact]
    public void ReferenceZipReader_OnTheSharedInflater_AnswersExactlyAsBefore()
    {
        var read = _reader.Read("site.zip", Zip(("site/index.html", Text("<p>")), ("site/css/a.css", Text("a{}"))));
        var over = _reader.Read("site.zip", Zip(Enumerable.Range(0, 3).Select(i => ($"site/{i}.bin", new byte[10L * 1024 * 1024])).ToArray()));

        read.Files.Select(f => (f.Path, System.Text.Encoding.UTF8.GetString(f.Content))).Should()
            .Equal(("site/index.html", "<p>"), ("site/css/a.css", "a{}"));
        read.Refusal.Should().BeNull();
        over.Refusal.Should().Be("'site/2.bin' takes 'site.zip' over the 25 MB per-file or 25 MB set limit once unpacked.");
    }

    [Fact]
    public void ZipEntryChecksum_Of_IsTheIeeeCrc32()
    {
        new ZipEntryChecksum().Of(Text("123456789")).Should().Be(0xCBF43926u);
    }

    [Fact]
    public void ReferenceZipReader_NotAnArchive_IsRefusedNamingIt()
    {
        _reader.Read("broken.zip", Text("not a zip")).Refusal.Should().Contain("'broken.zip'");
        ReferenceZipReader.IsArchive("x/Site.ZIP").Should().BeTrue();
    }
}
