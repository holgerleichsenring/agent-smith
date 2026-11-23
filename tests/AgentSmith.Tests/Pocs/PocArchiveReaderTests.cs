using System.Security.Cryptography;
using AgentSmith.Application.Services.Archives;
using AgentSmith.Application.Services.Pocs;
using AgentSmith.Contracts.Models.Configuration;
using FluentAssertions;
using static AgentSmith.Tests.Pocs.PocZips;

namespace AgentSmith.Tests.Pocs;

/// <summary>2026-11-23-933db: the upload admits from declared data, the intake decides on what was inflated; both drop
/// OS litter first and strip the one top folder before any rule.</summary>
public sealed class PocArchiveReaderTests
{
    private readonly PocArchiveReader _reader = Reader();
    private readonly PocArchiveLimits _limits = new();

    [Fact]
    public void Admit_AFinderMadeZip_PassesWithTheDockerfileAtTheRoot()
    {
        var zip = With(z =>
        {
            z.CreateEntry("MyApp/");
            Add(z, "MyApp/Dockerfile", Text(Dockerfile));
            Add(z, "MyApp/app.py", Text("print()"));
            Add(z, "MyApp/.DS_Store", Text("x"));
            Add(z, "__MACOSX/MyApp/._Dockerfile", Text("x"));
            Add(z, "__MACOSX/MyApp/._app.py", Text("x"));
        });

        var admission = _reader.Admit(zip, _limits);

        admission.Refusal.Should().BeNull();
        admission.Entries.Select(e => e.Path).Should().Equal("Dockerfile", "app.py");
        admission.Dockerfile.Should().Be(new PocDockerfile(8000, "CMD [\"python\", \"app.py\"]", "python:3.12-slim"));
        admission.UnpackedBytes.Should().Be(Text(Dockerfile).Length + Text("print()").Length);
    }

    [Fact]
    public void Admit_FilesWithNoSharedTopFolder_KeepTheirPaths()
    {
        var admission = _reader.Admit(Zip(("Dockerfile", Text(Dockerfile)), ("src/app.py", Text("x"))), _limits);

        admission.Entries.Select(e => e.Path).Should().Equal("Dockerfile", "src/app.py");
    }

    [Fact]
    public void Admit_NoRootDockerfile_IsRefused()
    {
        var admission = _reader.Admit(Zip(("app/src/Dockerfile", Text(Dockerfile)), ("app/x.py", Text("x"))), _limits);

        (admission.Check, admission.Refusal).Should().Be((PocArchiveChecks.Dockerfile,
            "the archive holds no Dockerfile at its root (after its one top folder); a PoC is built from it"));
    }

    [Fact]
    public void Admit_OverMaxBinaryMb_IsAdmitted_AndTheIntakeRefusesItNamingTheKey()
    {
        var binaries = Enumerable.Range(0, 3).Select(i => ($"app/{i}.bin", Binary(7 * 1024 * 1024))).ToArray();
        var zip = Stored([("app/Dockerfile", Text(Dockerfile)), .. binaries]);

        _reader.Admit(zip, _limits).IsRefused.Should().BeFalse("the central directory cannot tell a binary");
        var intake = _reader.Intake(zip, _limits);

        intake.Check.Should().Be(PocArchiveChecks.Binary);
        intake.Refusal.Should().StartWith("the archive holds 21 MB of binary files").And.Contain("over pocs.archive.max_binary_mb 20");
    }

    [Fact]
    public void Intake_AValidArchive_WritesTheManifestAfterTheStrip()
    {
        var intake = _reader.Intake(App(("app/static/logo.bin", Binary(100))), _limits);

        intake.Refusal.Should().BeNull();
        intake.Dockerfile!.Port.Should().Be(8000);
        intake.Manifest!.Files.Keys.Should().Equal("Dockerfile", "app.py", "static/logo.bin");
        var app = intake.Manifest.Files["app.py"];
        app.Should().Be(new PocManifestEntry(Convert.ToHexStringLower(SHA256.HashData(Text("print('hi')\n"))),
            new ZipEntryChecksum().Of(Text("print('hi')\n")), Text("print('hi')\n").Length));
    }

    [Fact]
    public void Intake_ADockerfileWithABlockedFlag_IsRefusedAgain()
    {
        var zip = Zip(("app/Dockerfile", Text("FROM alpine\nRUN --network=host wget x\nEXPOSE 80\n")));

        _reader.Intake(zip, _limits).Refusal.Should().StartWith("Dockerfile line 2: RUN --network=host is refused");
    }

    [Fact]
    public void Checks_Admission_IsEveryCheckButBinary_InDisplayOrder()
    {
        PocArchiveChecks.Admission.Should().Equal("paths", "kinds", "bounds", "forbidden", "dockerfile", "expose");
        PocArchiveChecks.Intake.Should().Equal("paths", "kinds", "bounds", "forbidden", "dockerfile", "expose", "binary");
    }

    private static byte[] Binary(int length)
    {
        var bytes = PocArchiveBoundsTests.Random(length);
        bytes[0] = 0;
        return bytes;
    }
}
