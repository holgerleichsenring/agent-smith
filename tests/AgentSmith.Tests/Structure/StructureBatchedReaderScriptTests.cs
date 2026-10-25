using System.Text;
using AgentSmith.Infrastructure.Services.Sandbox;
using AgentSmith.Tests.Architecture;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentSmith.Tests.Structure;

/// <summary>2026-10-13-ee83: the batch script run by a real shell, against real files.</summary>
[Trait("TestProcess", "serial")]
[Collection(ExternalProcessCollection.Name)]
public sealed class StructureBatchedReaderScriptTests
{
    [Fact]
    public async Task StructureBatchedReader_InProcessSandbox_RunsTheRealScript()
    {
        var dir = Directory.CreateTempSubdirectory("ee83-").FullName;
        await using var inner = new InProcessSandbox("ee83", dir, ownsWorkDir: true, InProcessStepEnvironment.Inherit, NullLogger.Instance);
        var written = Write(dir);
        var sandbox = new BatchProbeSandbox(inner);

        var contents = await StructureBatchedReaderTests.Read(sandbox, [.. written.Keys, "missing.cs"]);

        contents.Should().BeEquivalentTo(written.Append(new("missing.cs", null)));
        sandbox.BatchSteps.Should().Be(2, "the second large file overran the first step's budget and went to the next");
        sandbox.ReadSteps.Should().Be(1, "only the missing file is read on its own");
    }

    // Two large files cannot share one step's budget, so the script skips the second.
    private static Dictionary<string, string?> Write(string dir)
    {
        var large = new string('x', 400_000);
        var files = new Dictionary<string, string?>
        {
            ["src/A.cs"] = "class A\n{\n}\n", ["src/with space/B.cs"] = "class B {}", ["-dash.cs"] = "dash",
            ["empty.cs"] = string.Empty, ["umlaut.cs"] = "// Grüße\n", ["L1.cs"] = large, ["L2.cs"] = large,
        };
        foreach (var (path, content) in files) WriteFile(dir, path, Encoding.UTF8.GetBytes(content!));
        WriteFile(dir, "bom.cs", [0xEF, 0xBB, 0xBF, .. Encoding.UTF8.GetBytes("bom")]);
        WriteFile(dir, "bin.dat", [0xFF, 0xFE, 0x00, 0x01]);
        files["bom.cs"] = "bom";
        files["bin.dat"] = null;
        return files;
    }

    private static void WriteFile(string dir, string path, byte[] bytes)
    {
        var full = Path.Combine(dir, path);
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        File.WriteAllBytes(full, bytes);
    }
}
