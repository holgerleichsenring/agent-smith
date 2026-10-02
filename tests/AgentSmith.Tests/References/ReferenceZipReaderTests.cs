using AgentSmith.Server.Services.References;
using FluentAssertions;
using static AgentSmith.Tests.References.ReferenceUploadRequests;

namespace AgentSmith.Tests.References;

/// <summary>2026-10-01-283db: the one archive a set may arrive as is counted, not trusted.</summary>
public sealed class ReferenceZipReaderTests
{
    private readonly ReferenceZipReader _reader = new(new ReferencePathRule(), new ReferenceIgnoreList(), new ZipEntryChecksum());

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
    public void ReferenceZipReader_AnEntryOverTheSetBound_IsRefusedWhileInflating()
    {
        var big = Zip(("site/zeros.txt", new byte[ReferenceUploadLimits.MaxSetBytes + 1]));

        _reader.Read("site.zip", big).Refusal.Should().Contain("'site/zeros.txt'").And.Contain("25 MB set limit");
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
