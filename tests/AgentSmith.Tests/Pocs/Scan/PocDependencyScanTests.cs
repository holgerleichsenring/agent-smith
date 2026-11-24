using System.Text;
using AgentSmith.Application.Services.Builders;
using AgentSmith.Application.Services.Pocs.Scan;
using AgentSmith.Application.Services.Sandbox;
using AgentSmith.Contracts.Events;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Tests.Sandbox;
using FluentAssertions;
using Moq;

namespace AgentSmith.Tests.Pocs.Scan;

/// <summary>
/// 2026-11-23-933dc: the dependency scan runs the pinned image's /osv-scanner over the lockfiles written from memory —
/// a requirements file reduced to its exact pins — and never installs anything. No lockfile and exit 128 are notes; an
/// unknown exit, a tag-only image or a backend without images fail it.
/// </summary>
public sealed class PocDependencyScanTests : IAsyncDisposable
{
    private readonly OsvSandbox _sandbox = new();
    private SandboxSpec? _spec;

    [Fact]
    public async Task Scan_NoImageSet_RunsTheVerifiedDefault()
    {
        _sandbox.Report = Report("9.8");
        await ScanAsync(new PocScanConfig(), File("go.mod", "module x\n"));

        _spec!.ToolchainImage.Should().Be(PocScanConfig.DefaultOsvImage);
    }

    [Fact]
    public async Task Scan_RunsThePinnedScannerOverTheExactPinsOnly_NeverInstalling()
    {
        _sandbox.Report = Report("9.8");
        var result = await ScanAsync(new PocScanConfig { OsvImage = PocsConfigTests.PinnedOsvImage },
            File("requirements.txt", "--index-url http://x\nflask\nrequests>=2.0\ndjango==3.2.0\n"));

        result.Failure.Should().BeNull();
        _spec!.ToolchainImage.Should().Be(PocsConfigTests.PinnedOsvImage);
        _spec.Secrets.Should().Be(ResolvedSandboxSecrets.Empty);
        _sandbox.Scanner!.Args.Should().ContainInOrder("-L", "/work/lockfiles/requirements.txt");
        _sandbox.Steps.SelectMany(s => s.Args ?? Array.Empty<string>()).Should().NotContain("install");
        _sandbox.Written("lockfiles/requirements.txt").Should().Be("django==3.2.0\n");
        result.Notes.Should().ContainSingle().Which.Detail.Should().Contain("flask").And.Contain("requests>=2.0").And.Contain("--index-url");
        result.Vulnerabilities.Should().ContainSingle().Which.Score.Should().Be(9.8);
    }

    [Fact]
    public async Task Scan_NoLockfile_IsANote_AndNoSandboxStarts()
    {
        var result = await ScanAsync(Pinned(), File("app.py", "print(1)"));

        result.Notes.Should().ContainSingle().Which.Title.Should().Be("Dependencies not checked");
        _spec.Should().BeNull();
    }

    [Fact]
    public async Task Scan_Exit128_IsDependenciesNotChecked_Exit2Fails()
    {
        _sandbox.ExitCode = 128;
        (await ScanAsync(Pinned(), File("go.mod", "module x\n"))).Notes.Should().ContainSingle(n => n.Title == "Dependencies not checked");

        _sandbox.ExitCode = 2;
        (await ScanAsync(Pinned(), File("go.mod", "module x\n"))).Failure.Should().Contain("exited 2").And.Contain("osv-vulnerabilities.storage.googleapis.com");
    }

    [Fact]
    public async Task Scan_ATagOnlyImageOrABackendWithoutImages_Fails()
    {
        (await ScanAsync(new PocScanConfig { OsvImage = "ghcr.io/google/osv-scanner:v2" }, File("go.mod", "module x\n")))
            .Failure.Should().Contain("pocs.scan.osv_image");
        (await ScanAsync(Pinned(), new SandboxContainerRuntime(false), File("go.mod", "module x\n")))
            .Failure.Should().Contain("Kubernetes or Docker");
    }

    [Fact]
    public async Task Scan_AReportLargerThanOneRead_IsReadWhole()
    {
        var padding = string.Concat(Enumerable.Repeat(new string(' ', 200) + "\n", 8000));
        _sandbox.Report = Report("9.8").Replace("\"packages\":", padding + "\"packages\":", StringComparison.Ordinal);
        _sandbox.Report.Length.Should().BeGreaterThan(1_100_000);

        (await ScanAsync(Pinned(), File("requirements.txt", "django==3.2.0\n"))).Vulnerabilities.Should().ContainSingle();
    }

    private static PocScanConfig Pinned() => new() { OsvImage = PocsConfigTests.PinnedOsvImage };

    private Task<PocDependencyResult> ScanAsync(PocScanConfig config, params ReferenceSetFile[] files) =>
        ScanAsync(config, new SandboxContainerRuntime(true), files);

    private async Task<PocDependencyResult> ScanAsync(PocScanConfig config, SandboxContainerRuntime runtime, params ReferenceSetFile[] files)
    {
        var factory = new Mock<ISandboxFactory>();
        factory.Setup(f => f.CreateAsync(It.IsAny<SandboxSpec>(), It.IsAny<CancellationToken>()))
            .Callback<SandboxSpec, CancellationToken>((spec, _) => _spec = spec).ReturnsAsync(new NonDisposing(_sandbox));
        var readers = new SandboxFileReaderFactory();
        var ids = new PocFindingIds();
        var scan = new PocDependencyScan(factory.Object,
            new SandboxSpecBuilder(new StubSandboxResourceResolver(), Mock.Of<IAgentImageResolver>(r => r.Resolve(It.IsAny<ResolvedProject>()) == "agent:test")),
            Mock.Of<IRunContextAccessor>(r => r.CurrentRunId == "run-1"),
            new ReferenceSetMaterialiser(Mock.Of<IReferenceSetReader>(), readers, new SandboxBinaryFileWriter()),
            new PocScannedLockfiles(new PocLockfiles(), new PocRequirementsFilter(), ids), new PocOsvRun(),
            new PocOsvReportReader(readers), new OsvReportParser(), ids, runtime);
        return await scan.ScanAsync(new ResolvedProject { Name = "poc-ann" }, files, config, CancellationToken.None);
    }

    private static ReferenceSetFile File(string path, string text) => new(path, Encoding.UTF8.GetBytes(text));

    private static string Report(string severity) => $$"""
        {"results": [{"source": {"path": "/work/lockfiles/requirements.txt", "type": "lockfile"},
          "packages": [{"package": {"name": "django", "version": "3.2.0", "ecosystem": "PyPI"},
            "vulnerabilities": [{"id": "PYSEC-1", "summary": "SQL injection", "details": "long text", "affected": []}],
            "groups": [{"ids": ["PYSEC-1"], "aliases": ["CVE-1"], "max_severity": "{{severity}}"}]}]}]}
        """;

    public ValueTask DisposeAsync() => _sandbox.DisposeAsync();

    private sealed class NonDisposing(ISandbox inner) : ISandbox
    {
        public string JobId => inner.JobId;

        public Task<AgentSmith.Sandbox.Wire.StepResult> RunStepAsync(
            AgentSmith.Sandbox.Wire.Step step, IProgress<AgentSmith.Sandbox.Wire.StepEvent>? progress, CancellationToken ct) =>
            inner.RunStepAsync(step, progress, ct);

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
