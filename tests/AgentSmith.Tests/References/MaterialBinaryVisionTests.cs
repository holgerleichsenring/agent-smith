using System.Text;
using AgentSmith.Application.Models;
using AgentSmith.Application.Services.Browser;
using AgentSmith.Application.Services.Events;
using AgentSmith.Application.Services.Sandbox;
using AgentSmith.Application.Services.Tools;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Infrastructure.Services.Sandbox;
using AgentSmith.Sandbox.Wire;
using AgentSmith.Tests.DesignSources;
using AgentSmith.Tests.Sandbox;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentSmith.Tests.References;

/// <summary>
/// 2026-10-08-e8b9j: uploaded files reach a sandbox byte for byte without python — on the
/// in-process backend above all, which runs in the server and has no python3 — and an image inside
/// an upload is shown to the model on request.
/// </summary>
public sealed class MaterialBinaryVisionTests : IDisposable
{
    private static readonly byte[] Png = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00, 0x2A];
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "as-e8b9j-" + Guid.NewGuid().ToString("N"));

    public MaterialBinaryVisionTests() => Directory.CreateDirectory(_dir);

    public void Dispose()
    {
        try { Directory.Delete(_dir, recursive: true); } catch { /* best-effort */ }
    }

    [Fact]
    public async Task InProcess_WriteBytesWithoutPath_Refused()
    {
        await using var sandbox = InProcess();

        var result = await sandbox.RunStepAsync(new Step(Step.CurrentSchemaVersion, Guid.NewGuid(), StepKind.WriteBytes,
            Content: "AA=="), null, CancellationToken.None);

        result.ExitCode.Should().NotBe(0);
        result.ErrorMessage.Should().Contain("WriteBytes step requires non-empty Path");
    }

    [Fact]
    public async Task WriteBytes_InProcessChunks_20MBFileByteIdentical()
    {
        await using var sandbox = new CountingSandbox(InProcess());
        var content = new byte[20 * 1024 * 1024];
        new Random(7).NextBytes(content);

        var failure = await new SandboxBinaryFileWriter().WriteAsync(sandbox, "docs", "big.pdf", content, CancellationToken.None);

        failure.Should().BeNull();
        (await File.ReadAllBytesAsync(Path.Combine(_dir, "docs", "big.pdf"))).Should().Equal(content);
        sandbox.Steps.Should().HaveCount(5, "4 MB a step").And.OnlyContain(s => s.Kind == StepKind.WriteBytes);
        Directory.GetFiles(Path.Combine(_dir, "docs")).Should().ContainSingle("the temporary name was moved into place");
    }

    [Fact]
    public void WriteBytesMaxBytes_EqualsReferenceSetFileBound() =>
        SizeLimits.WriteBytesMaxBytes.Should().Be(ReferenceSetLimits.MaxFileBytes);

    [Fact]
    public async Task SandboxBinaryFileWriter_ChunksOver4MB_NoShellStep()
    {
        var sandbox = new InMemoryFileSandbox();

        await new SandboxBinaryFileWriter().WriteAsync(sandbox, "d", "x.bin", new byte[9 * 1024 * 1024], CancellationToken.None);

        sandbox.Shells.Should().BeEmpty();
        sandbox.PythonRuns.Should().Be(0);
        sandbox.Writes.Should().Be(3);
        sandbox.Files["/work/d/x.bin"].Should().HaveCount(9 * 1024 * 1024);
    }

    [Fact]
    public async Task ReferenceSetMaterialiser_PngInProcess_ByteIdentical()
    {
        await using var sandbox = InProcess();

        await Materialiser().WriteUnderAsync(sandbox,
            [new ReferenceSetFile("site/logo.png", Png), new ReferenceSetFile("site/index.html", Encoding.UTF8.GetBytes("<h1>"))],
            "ref", CancellationToken.None);

        (await File.ReadAllBytesAsync(Path.Combine(_dir, "ref", "site", "logo.png"))).Should().Equal(Png);
        (await File.ReadAllTextAsync(Path.Combine(_dir, "ref", "site", "index.html"))).Should().Be("<h1>");
    }

    [Fact]
    public async Task ReferenceSetMaterialiser_Utf8Text_StaysOnWriteFile()
    {
        var sandbox = new CountingSandbox(new InMemoryFileSandbox());

        await Materialiser().WriteUnderAsync(sandbox, [new ReferenceSetFile("a/app.py", Encoding.UTF8.GetBytes("print(1)"))],
            string.Empty, CancellationToken.None);

        sandbox.Steps.Should().NotContain(s => s.Kind == StepKind.WriteBytes, "every agent reads a WriteFile");
    }

    [Fact]
    public async Task VisualComparisonRecorder_DiffImages_WrittenWithoutPython()
    {
        var repo = new InMemoryFileSandbox();
        var pipeline = new PipelineContext();
        pipeline.Set(ContextKeys.RunId, "run-1");

        var recorded = await new VisualComparisonRecorder(new SandboxBinaryFileWriter(), NullLogger<VisualComparisonRecorder>.Instance)
            .RecordAsync(pipeline, repo, new VisualComparison("a", "b", [], []), [("desktop", Png)], CancellationToken.None);

        repo.PythonRuns.Should().Be(0);
        recorded.Images.Should().ContainSingle();
        repo.Files["/work/" + recorded.Images[0]].Should().Equal(Png);
    }

    [Fact]
    public void SandboxStepFacts_WriteBytes_HashesNothing()
    {
        var step = new Step(Step.CurrentSchemaVersion, Guid.NewGuid(), StepKind.WriteBytes, Path: "a/b.png", Content: "AAAA");

        SandboxStepFacts.ContentHash(step, null).Should().BeNull();
        SandboxStepFacts.Summarize(step).Should().Be("a/b.png");
    }

    [Fact]
    public void SourceScope_WriteBytes_IsRefused() =>
        SourceScopeRefusal.Unless(new Step(Step.CurrentSchemaVersion, Guid.NewGuid(), StepKind.WriteBytes, Path: "x", Content: "AA=="),
            new SourceScopeWritePolicy([])).Should().NotBeNull();

    private InProcessSandbox InProcess() => new("e8b9j", _dir, ownsWorkDir: false, NullLogger.Instance);

    private static ReferenceSetMaterialiser Materialiser() =>
        new(new NoReferenceSetReader(), new SandboxFileReaderFactory(), new SandboxBinaryFileWriter());

    /// <summary>Passes every step on and keeps it.</summary>
    private sealed class CountingSandbox(ISandbox inner) : ISandbox
    {
        public List<Step> Steps { get; } = [];
        public string JobId => inner.JobId;

        public Task<StepResult> RunStepAsync(Step step, IProgress<StepEvent>? progress, CancellationToken cancellationToken)
        {
            Steps.Add(step);
            return inner.RunStepAsync(step, progress, cancellationToken);
        }

        public ValueTask DisposeAsync() => inner.DisposeAsync();
    }
}
