using System.IO.Compression;
using AgentSmith.Application.Services.Pocs;
using AgentSmith.Contracts.Models.Configuration;
using FluentAssertions;
using static AgentSmith.Tests.Pocs.PocZips;

namespace AgentSmith.Tests.Pocs;

/// <summary>2026-11-23-933db: paths and entry kinds of a PoC archive, refused at the upload from the central directory
/// alone, each naming the entry and why.</summary>
public sealed class PocArchivePathRulesTests
{
    private readonly PocArchiveReader _reader = Reader();
    private readonly PocArchiveLimits _limits = new();

    [Theory]
    [InlineData("../evil.sh", "climbs out")]
    [InlineData("/etc/passwd", "absolute path")]
    [InlineData("..\\evil.sh", "climbs out")]
    [InlineData("C:\\evil.sh", "names a drive")]
    [InlineData("app//x.py", "empty path segment")]
    public void Admit_AHostilePath_IsRefusedNamingIt(string path, string why)
    {
        var admission = _reader.Admit(App((path, Text("x"))), _limits);

        admission.Check.Should().Be(PocArchiveChecks.Paths);
        admission.Refusal.Should().Contain(why);
        admission.Entries.Should().BeEmpty();
    }

    [Fact]
    public void Admit_ANulInAPath_IsRefused()
    {
        var zip = Renaming(App(("app/aXb.py", Text("x"))), (byte)'X', 0);

        _reader.Admit(zip, _limits).Refusal.Should().Contain("NUL");
    }

    [Fact]
    public void Admit_ACaseClash_IsRefused()
    {
        var admission = _reader.Admit(App(("app/README.md", Text("a")), ("app/Readme.md", Text("b"))), _limits);

        (admission.Check, admission.Refusal).Should().Be((PocArchiveChecks.Paths, "'Readme.md' appears twice in the set."));
    }

    [Fact]
    public void Admit_APathOverMaxPathChars_IsRefusedNamingTheKey()
    {
        var admission = _reader.Admit(App(("app/" + new string('a', 241), Text("x"))), _limits);

        admission.Refusal.Should().Contain("is 241 characters long").And.Contain("over pocs.archive.max_path_chars 240");
    }

    [Theory]
    [InlineData(0xA1FFu, "symbolic link")]
    [InlineData(0x11A4u, "named pipe (fifo)")]
    public void Admit_ASymlinkOrFifo_IsRefusedNamingItsKind(uint mode, string kind)
    {
        var zip = With(z =>
        {
            Add(z, "app/Dockerfile", Text(Dockerfile));
            z.CreateEntry("app/link").ExternalAttributes = unchecked((int)(mode << 16));
        });

        var admission = _reader.Admit(zip, _limits);

        (admission.Check, admission.Refusal).Should().Be((PocArchiveChecks.Kinds,
            $"'link' is a {kind}, not a regular file; a PoC's source holds files and folders only, because a link or a "
            + "special file can reach outside it."));
    }

    [Fact]
    public void Admit_ARegularUnixFile_Passes()
    {
        var zip = With(z =>
        {
            Add(z, "app/Dockerfile", Text(Dockerfile));
            z.CreateEntry("app/run.sh", CompressionLevel.Optimal).ExternalAttributes = unchecked((int)(0x81EDu << 16));
        });

        _reader.Admit(zip, _limits).IsRefused.Should().BeFalse();
    }

    [Fact]
    public void Admit_AnEncryptedEntry_IsRefused()
    {
        var admission = _reader.Admit(Encrypting(App(("app/secret.txt", Text("x"))), "app/secret.txt"), _limits);

        (admission.Check, admission.Refusal).Should().Be((PocArchiveChecks.Kinds,
            "'secret.txt' is encrypted; an encrypted entry cannot be read, so nothing could check it."));
    }

    [Fact]
    public void Admit_NotAZip_IsRefusedAsUnreadable()
    {
        var admission = _reader.Admit(Text("not a zip"), _limits);

        admission.Refusal.Should().StartWith("the upload is not a readable ZIP archive");
        _reader.Intake(Text("not a zip"), _limits).IsRefused.Should().BeTrue();
    }
}
