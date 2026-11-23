using System.IO.Compression;
using AgentSmith.Application.Services.Archives;
using FluentAssertions;
using static AgentSmith.Tests.References.ReferenceUploadRequests;

namespace AgentSmith.Tests.Archives;

/// <summary>2026-11-23-933db: the counted inflate both archive readers share — the bound is the caller's, a lying
/// declared length is caught, an honest entry arrives whole.</summary>
public sealed class BoundedZipInflaterTests
{
    private readonly BoundedZipInflater _inflater = new(new ZipEntryChecksum());

    [Fact]
    public void Inflate_AnHonestEntryWithinTheBound_ArrivesWhole()
    {
        var inflated = Inflate(Zip(("a.txt", Text("hello"))), "a.txt", 1024);

        inflated.IsRefused.Should().BeFalse();
        inflated.Bytes.Should().Equal(Text("hello"));
    }

    [Fact]
    public void Inflate_AnEntryOverTheBound_StopsThere()
    {
        Inflate(Zip(("zeros.bin", new byte[64 * 1024])), "zeros.bin", 1024).Fault.Should().Be(ZipInflationFault.OverBound);
    }

    [Fact]
    public void Inflate_ALyingDeclaredLength_StopsAtTheBound()
    {
        // The entry declares 10 bytes and holds 1 MB: what is read is counted against the caller's bound of 5 bytes,
        // never trusted to the claim.
        var bomb = Lying(Zip(("zeros.bin", new byte[1024 * 1024])), claimed: 10);

        Inflate(bomb, "zeros.bin", 5).Fault.Should().Be(ZipInflationFault.OverBound);
    }

    [Fact]
    public void Inflate_AnEntryCutShortAtItsClaim_IsRefusedByItsChecksum()
    {
        var bomb = Lying(Zip(("zeros.bin", new byte[1024 * 1024])), claimed: 10);

        var inflated = Inflate(bomb, "zeros.bin", 2 * 1024 * 1024);

        inflated.Fault.Should().Be(ZipInflationFault.ChecksumMismatch);
        inflated.Bytes.Should().BeEmpty();
    }

    private ZipInflation Inflate(byte[] zip, string path, long bound)
    {
        using var archive = new ZipArchive(new MemoryStream(zip), ZipArchiveMode.Read);
        return _inflater.Inflate(archive.GetEntry(path)!, bound);
    }
}
