using AgentSmith.Application.Services.Sandbox;
using AgentSmith.Application.Services.Structure;
using FluentAssertions;

namespace AgentSmith.Tests.Structure;

public sealed class StructureBatchedReaderTests
{
    internal static Task<IReadOnlyDictionary<string, string?>> Read(BatchProbeSandbox sandbox, params string[] paths) =>
        StructureBaselineStubs.Batched().ReadAsync(
            sandbox, new SandboxFileReaderFactory().Create(sandbox), paths, CancellationToken.None);

    private static string Line(int index, byte[] bytes) => $"{index} {Convert.ToBase64String(bytes)}\n";

    [Fact]
    public async Task StructureBatchedReader_Chunk_ReturnsEveryFile()
    {
        var repo = new RepoSandbox();
        var paths = Enumerable.Range(0, 1200).Select(i => $"src/F{i}.cs").ToArray();
        foreach (var path in paths) repo.Files[path] = $"class {Path.GetFileNameWithoutExtension(path)} {{ }}\n";
        var sandbox = new BatchProbeSandbox(repo);

        var contents = await Read(sandbox, paths);

        contents.Should().BeEquivalentTo(repo.Files);
        sandbox.BatchSteps.Should().Be(3, "five hundred paths go in one step");
        sandbox.ReadSteps.Should().Be(0);
    }

    [Theory]
    [InlineData(0, "0 YQ==\n1 Yg==\n")]
    [InlineData(1, "0 YQ==\n1 Yg==\nend 2\n")]
    [InlineData(0, "0 YQ==\n1 Yg==\nend 3\n")]
    public async Task StructureBatchedReader_MissingMarker_FallsBackPerFile(int exitCode, string output)
    {
        var repo = new RepoSandbox { Files = { ["a.cs"] = "per-file a", ["b.cs"] = "per-file b" } };
        var sandbox = new BatchProbeSandbox(repo) { Answer = (exitCode, output) };

        var contents = await Read(sandbox, "a.cs", "b.cs");

        contents.Should().BeEquivalentTo(new Dictionary<string, string?> { ["a.cs"] = "per-file a", ["b.cs"] = "per-file b" });
        sandbox.ReadSteps.Should().Be(2, "a cut or failed batch trusts none of its lines");
    }

    [Fact]
    public async Task StructureBatchedReader_FileWithoutLine_IsReadOnItsOwn()
    {
        var repo = new RepoSandbox { Files = { ["a.cs"] = "per-file a", ["b.cs"] = "per-file b" } };
        var sandbox = new BatchProbeSandbox(repo) { Answer = (0, "0 YQ==\nend 2\n") };

        var contents = await Read(sandbox, "a.cs", "b.cs");

        contents.Should().BeEquivalentTo(new Dictionary<string, string?> { ["a.cs"] = "a", ["b.cs"] = "per-file b" });
        sandbox.ReadSteps.Should().Be(1);
    }

    [Fact]
    public async Task StructureBatchedReader_OversizedOrBinary_CountedUnreadable()
    {
        var bom = new byte[] { 0xEF, 0xBB, 0xBF, (byte)'x' };
        var output = Line(0, [0xFF, 0xFE, 0x00]) + Line(1, new byte[1_048_577]) + Line(2, bom) + "end 3\n";
        var sandbox = new BatchProbeSandbox(new RepoSandbox()) { Answer = (0, output) };

        var contents = await Read(sandbox, "bin.dat", "big.cs", "bom.cs");

        contents.Should().BeEquivalentTo(new Dictionary<string, string?> { ["bin.dat"] = null, ["big.cs"] = null, ["bom.cs"] = "x" });
        sandbox.ReadSteps.Should().Be(0, "a refusal is an answer, as the container's ReadFile gives it");
    }
}
