using System.Text;
using AgentSmith.Application.Services.Sandbox;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Sandbox.Wire;
using AgentSmith.Tests.Sandbox;
using FluentAssertions;

namespace AgentSmith.Tests.References;

/// <summary>
/// 2026-10-01-283dc: an uploaded website as a read-only address — filled on the first read, at
/// each file's relative path, binaries decoded in one step, never written to, and taken back
/// whole by the conversation's next turn.
/// </summary>
public sealed class ReferenceSetSandboxTests
{
    private static readonly byte[] Png = [0x89, 0x50, 0x4E, 0x47, 0x00, 0xFF, 0x10];
    private readonly ReferenceSandboxFixture _fixture = new();

    [Fact]
    public async Task ReferenceSetSandbox_FirstRead_MaterialisesEveryFileAtItsRelativePath()
    {
        _fixture.Set.AddRange([Text("site/index.html", "<h1>Hi</h1>"), Text("site/css/site.css", "h1{color:#c0ffee}"),
            new ReferenceSetFile("site/img/logo.png", Png)]);
        await using var scope = _fixture.Open(Holds.None());

        var read = await scope.RunStepAsync(At(StepKind.ReadFile, "site/css/site.css"), null, CancellationToken.None);

        read.OutputContent.Should().Be("h1{color:#c0ffee}");
        var files = _fixture.Spawned.Single().Files;
        files["/work/site/index.html"].Should().Equal(Encoding.UTF8.GetBytes("<h1>Hi</h1>"));
        files["/work/site/img/logo.png"].Should().Equal(Png, "a binary file is decoded back to its bytes");
        files.Keys.Should().NotContain(k => k.EndsWith(ReferenceSetMaterialiser.EncodedSuffix));
        scope.RepoName.Should().Be("reference:site");
        scope.ResolvedSha.Should().HaveLength(64, "the address carries the set's content hash");
    }

    [Fact]
    public async Task ReferenceSetMaterialiser_FiftyBinaryFiles_DecodesInOneStep()
    {
        _fixture.Set.AddRange(Enumerable.Range(0, 50).Select(i => new ReferenceSetFile($"site/img/{i}.png", Png)));
        await using var scope = _fixture.Open(Holds.None());

        await scope.MaterializeAsync(CancellationToken.None);

        var sandbox = _fixture.Spawned.Single();
        sandbox.PythonRuns.Should().Be(1, "fifty files decoded file by file would be fifty round trips");
        sandbox.Files.Keys.Count(k => k.EndsWith(".png")).Should().Be(50);
    }

    // 2026-10-02-075da: text is what decodes, whatever the extension.
    [Fact]
    public async Task ReferenceSetMaterialiser_PyAndDockerfile_AreWrittenAsText()
    {
        _fixture.Set.AddRange([Text("app/app.py", "print(1)"), Text("app/Dockerfile", "FROM x"),
            new ReferenceSetFile("app/blob.bin", [0x41, 0x00, 0x42])]);
        await using var scope = _fixture.Open(Holds.None());

        await scope.MaterializeAsync(CancellationToken.None);

        var sandbox = _fixture.Spawned.Single();
        sandbox.Files["/work/app/app.py"].Should().Equal(Encoding.UTF8.GetBytes("print(1)"));
        sandbox.Files["/work/app/blob.bin"].Should().Equal([0x41, 0x00, 0x42], "a NUL byte sends a file encoded");
        sandbox.PythonRuns.Should().Be(1);
    }

    [Fact]
    public async Task ReferenceSetMaterialiser_AnUploadedB64File_IsLeftAsIs()
    {
        _fixture.Set.Add(Text("app/logo.b64", "aGVsbG8="));
        await using var scope = _fixture.Open(Holds.None());

        await scope.MaterializeAsync(CancellationToken.None);

        var sandbox = _fixture.Spawned.Single();
        sandbox.Files["/work/app/logo.b64"].Should().Equal(Encoding.UTF8.GetBytes("aGVsbG8="));
        sandbox.PythonRuns.Should().Be(0);
    }

    [Fact]
    public async Task ReferenceSetMaterialiser_TextThatIsNotUtf8_GoesInEncoded()
    {
        var latin1 = new byte[] { 0x61, 0xE9, 0x7B, 0x7D };
        _fixture.Set.Add(new ReferenceSetFile("site/old.css", latin1));
        await using var scope = _fixture.Open(Holds.None());

        await scope.MaterializeAsync(CancellationToken.None);

        _fixture.Spawned.Single().Files["/work/site/old.css"].Should().Equal(latin1, "no byte is lost to a text decode");
    }

    [Fact]
    public async Task ReferenceSetSandbox_WriteFileStep_IsRefused()
    {
        _fixture.Set.Add(Text("site/index.html", "<h1>"));
        await using var scope = _fixture.Open(Holds.None());

        var write = await scope.RunStepAsync(
            At(StepKind.WriteFile, "site/index.html") with { Content = "changed" }, null, CancellationToken.None);

        write.ExitCode.Should().NotBe(0);
        _fixture.Spawned.Should().BeEmpty("a refused step spawns nothing");
    }

    // 2026-10-02-075dc: the container holds a copy of the upload and nothing else; a shell is served.
    [Fact]
    public async Task ReferenceSetSandbox_ShellRunStep_IsServed()
    {
        _fixture.Set.Add(Text("app/app.py", "print(1)"));
        await using var scope = _fixture.Open(Holds.None());

        var shell = await scope.RunStepAsync(
            new Step(Step.CurrentSchemaVersion, Guid.NewGuid(), StepKind.Run, Command: "/bin/sh", Args: ["-c", "python3 app/app.py"]),
            null, CancellationToken.None);

        shell.ExitCode.Should().Be(0);
        _fixture.Spawned.Single().Shells.Should().Equal("python3 app/app.py");
    }

    [Fact]
    public async Task ReferenceSetSandbox_SpawnSpec_CarriesNoProjectSecrets()
    {
        _fixture.Set.Add(Text("app/app.py", "print(1)"));
        await using var scope = _fixture.Open(Holds.None());

        await scope.MaterializeAsync(CancellationToken.None);

        _fixture.Specs.Single().Secrets.Should().Be(ResolvedSandboxSecrets.Empty);
    }

    [Fact]
    public async Task ReferenceSetSandbox_SecondTurn_TakesTheHeldSandboxBackWithoutRewriting()
    {
        _fixture.Set.AddRange([Text("site/index.html", "<h1>"), new ReferenceSetFile("site/logo.png", Png)]);
        var holds = Holds.Live();
        var first = _fixture.Open(holds);
        await first.MaterializeAsync(CancellationToken.None);
        var sha = first.ResolvedSha;
        await first.DisposeAsync();
        var writes = _fixture.Spawned.Single().Writes;

        await using var second = _fixture.Open(holds);
        var read = await second.RunStepAsync(At(StepKind.ReadFile, "site/index.html"), null, CancellationToken.None);

        read.OutputContent.Should().Be("<h1>");
        _fixture.Spawned.Should().ContainSingle("the second turn reads through the sandbox the first left");
        _fixture.Spawned.Single().Writes.Should().Be(writes, "a held set is never rewritten");
        _fixture.SetReads.Should().Be(1, "and not even read from the store again");
        second.ResolvedSha.Should().Be(sha);
    }

    [Fact]
    public async Task NoReferenceSetReader_FilesAsync_HasNoSets()
    {
        (await new NoReferenceSetReader().FilesAsync("s", "set", CancellationToken.None)).Should().BeEmpty();
    }

    private static ReferenceSetFile Text(string path, string text) => new(path, Encoding.UTF8.GetBytes(text));

    private static Step At(StepKind kind, string path) =>
        new(Step.CurrentSchemaVersion, Guid.NewGuid(), kind, Path: path);
}
