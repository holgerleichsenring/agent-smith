using AgentSmith.Application.Services.Pocs;
using AgentSmith.Contracts.Models.Configuration;
using FluentAssertions;
using static AgentSmith.Tests.Pocs.PocZips;

namespace AgentSmith.Tests.Pocs;

/// <summary>2026-11-23-933db: the pocs.archive bounds, read from what the central directory declares at the upload —
/// each refusal naming the value, the key and why the bound exists.</summary>
public sealed class PocArchiveBoundsTests
{
    private const uint Mb = 1024 * 1024;
    private readonly PocArchiveReader _reader = Reader();
    private readonly PocArchiveLimits _limits = new();

    [Fact]
    public void Admit_2001Entries_IsRefusedNamingMaxEntries()
    {
        var files = Enumerable.Range(0, 2000).Select(i => ($"app/f{i}.txt", Text("x"))).ToArray();

        _reader.Admit(App(files), _limits).Refusal.Should()
            .Be("the archive holds 2002 files, over pocs.archive.max_entries 2000, the bound on how many files one PoC may carry");
    }

    [Fact]
    public void Admit_AnElevenMegabyteFile_IsRefusedNamingMaxFileMb()
    {
        var zip = Declaring(App(("app/big.bin", Text("x"))), "app/big.bin", 11 * Mb);

        var admission = _reader.Admit(zip, _limits);

        (admission.Check, admission.Refusal).Should().Be((PocArchiveChecks.Bounds,
            "'big.bin' unpacks to 11 MB, over pocs.archive.max_file_mb 10, the bound on one file of a PoC's source"));
    }

    [Fact]
    public void Admit_101MegabytesUnpacked_IsRefusedNamingMaxUnpackedMb()
    {
        var zip = App([.. Enumerable.Range(0, 11).Select(i => ($"app/{i}.bin", Text("x")))]);
        for (var i = 0; i < 11; i++) zip = Declaring(zip, $"app/{i}.bin", 9 * Mb + Mb / 2);

        _reader.Admit(zip, _limits).Refusal.Should().StartWith("the archive unpacks to 104.5 MB, over pocs.archive.max_unpacked_mb 100");
    }

    [Fact]
    public void Admit_AnEntryUnpacking101To1_IsRefusedNamingMaxRatio()
    {
        var zip = Stored(("app/Dockerfile", Text(Dockerfile)), ("app/x.bin", Random(1000)));
        zip = Declaring(zip, "app/x.bin", (uint)(101 * CompressedSize(zip, "app/x.bin")));

        _reader.Admit(zip, _limits).Refusal.Should().Be("'x.bin' unpacks 101:1, over pocs.archive.max_ratio 100, the bound "
            + "that stops a ZIP bomb before it fills the server's memory");
    }

    [Fact]
    public void Admit_AZipOverMaxZipMb_IsRefusedNamingIt()
    {
        var admission = _reader.Admit(App(("app/r.bin", Random(2 * (int)Mb))), new PocArchiveLimits { MaxZipMb = 1 });

        admission.Refusal.Should().StartWith("the archive is 2 MB, over pocs.archive.max_zip_mb 1");
    }

    [Fact]
    public void Intake_AnEntryLyingAboutItsSize_IsRefusedWhileInflating()
    {
        // Declared 10 bytes, holding 2 MB: the upload's declared bounds pass; the intake's counted inflate does not.
        var zip = Declaring(App(("app/zeros.bin", new byte[2 * Mb])), "app/zeros.bin", 10);
        var limits = new PocArchiveLimits { MaxFileMb = 1, MaxRatio = int.MaxValue };

        _reader.Admit(zip, limits).IsRefused.Should().BeFalse();
        var intake = _reader.Intake(zip, limits);
        (intake.Check, intake.Manifest).Should().Be((PocArchiveChecks.Bounds, null));
        intake.Refusal.Should().Contain("'zeros.bin'");
    }

    internal static byte[] Random(int length)
    {
        var bytes = new byte[length];
        new Random(933).NextBytes(bytes);
        return bytes;
    }
}
