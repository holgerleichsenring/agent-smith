using AgentSmith.Application.Models;
using AgentSmith.Application.Services.Archives;
using AgentSmith.Application.Services.Builders;
using AgentSmith.Application.Services.Handlers;
using AgentSmith.Application.Services.Pocs;
using AgentSmith.Application.Services.Pocs.Scan;
using AgentSmith.Application.Services.Sandbox;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Events;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Pocs;
using AgentSmith.Contracts.Runs;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Contracts.Services;
using AgentSmith.Tests.Sandbox;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AgentSmith.Tests.Pocs.Scan;

/// <summary>
/// 2026-11-23-933dc: the code scan step with its real parts — the manifest re-read, the in-process pattern scan over test
/// patterns, the dependency scan over an <see cref="OsvSandbox"/>, the rules and the explanation — and the scan store
/// held in memory. <see cref="Revision"/> stores a ZIP as a validated revision with its manifest.
/// </summary>
internal sealed class PocScanRig : IAsyncDisposable
{
    private readonly PocPatternsDirectory _patterns = new();

    internal FakeScanStore Store { get; } = new();

    internal OsvSandbox Osv { get; } = new();

    internal ExplainChats Chats { get; } = new();

    internal List<RunEvent> Events { get; } = [];

    internal void Revision(int number, byte[] zip, string state = PocRevisionStates.Validated)
    {
        var manifest = PocZips.Reader().Intake(zip, new PocArchiveLimits()).Manifest!.ToJson();
        Store.Rows[number] = new FakeScanStore.Row { State = state, Archive = zip, ManifestJson = manifest };
    }

    internal PocCodeScanHandler Handler() => new(Store, Gate());

    internal PocRevisionFiles Files()
    {
        var archives = new ServiceCollection().AddArchiveRules().BuildServiceProvider();
        return new PocRevisionFiles(Store, new PocManifestFiles(archives.GetRequiredService<PocArchiveLayout>(), archives.GetRequiredService<BoundedZipInflater>()));
    }

    internal PocCodeScanContext Context(int revision, bool overrule = false, string? reason = null, IReadOnlyList<string>? ids = null)
    {
        var pipeline = new PipelineContext();
        pipeline.Set(ContextKeys.RunId, "run-1");
        return new PocCodeScanContext(pipeline, new ResolvedProject { Name = "poc-ann", Agent = new AgentConfig { Type = "claude" } },
            new PocSeeds(7, revision, "ann-sub", overrule, reason, ids ?? []));
    }

    private PocScanGate Gate()
    {
        var events = new Mock<IEventPublisher>();
        events.Setup(e => e.PublishAsync(It.IsAny<RunEvent>(), It.IsAny<CancellationToken>()))
            .Callback<RunEvent, CancellationToken>((e, _) => Events.Add(e)).Returns(Task.CompletedTask);
        var scanner = new PocCodeScanner(Files(), _patterns.Scan(), Dependencies(), new PocBlockRules(
            new PocSecretRule(new PocFindingIds()), new PocVulnerabilityRule(new PocFindingIds())));
        var explanation = new PocScanExplanation(Chats, new PocScanPrompt(), new SecretMasker(new LoadedSecretValues(new AgentSmithConfig())),
            Mock.Of<IRunContextAccessor>(), NullLogger<PocScanExplanation>.Instance);
        return new PocScanGate(scanner, explanation, Store, new PocOverruledPublisher(events.Object, TimeProvider.System), Pinned());
    }

    private PocDependencyScan Dependencies()
    {
        var factory = new Mock<ISandboxFactory>();
        factory.Setup(f => f.CreateAsync(It.IsAny<SandboxSpec>(), It.IsAny<CancellationToken>())).ReturnsAsync(() => new KeptSandbox(Osv));
        var readers = new SandboxFileReaderFactory();
        var ids = new PocFindingIds();
        return new PocDependencyScan(factory.Object,
            new SandboxSpecBuilder(new StubSandboxResourceResolver(), Mock.Of<IAgentImageResolver>(r => r.Resolve(It.IsAny<ResolvedProject>()) == "agent:test")),
            Mock.Of<IRunContextAccessor>(), new ReferenceSetMaterialiser(Mock.Of<IReferenceSetReader>(), readers, new SandboxBinaryFileWriter()),
            new PocScannedLockfiles(new PocLockfiles(), new PocRequirementsFilter(), ids), new PocOsvRun(), new PocOsvReportReader(readers),
            new OsvReportParser(), ids, new SandboxContainerRuntime(true));
    }

    /// <summary>The live pocs block with a pinned scanner image.</summary>
    internal static PocsConfigSource Pinned(PocScanConfig? scan = null)
    {
        var config = new AgentSmithConfig { Pocs = new PocsConfig { Scan = scan ?? new PocScanConfig { OsvImage = PocsConfigTests.PinnedOsvImage } } };
        return new PocsConfigSource(Mock.Of<IConfigurationLoader>(l => l.LoadConfig(It.IsAny<string>()) == config), new ServerContext("agentsmith.yml"));
    }

    /// <summary>The scanner sandbox outlives each scan, so a test reads what was written into it.</summary>
    private sealed class KeptSandbox(ISandbox inner) : ISandbox
    {
        public string JobId => inner.JobId;

        public Task<AgentSmith.Sandbox.Wire.StepResult> RunStepAsync(
            AgentSmith.Sandbox.Wire.Step step, IProgress<AgentSmith.Sandbox.Wire.StepEvent>? progress, CancellationToken ct) =>
            inner.RunStepAsync(step, progress, ct);

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    public async ValueTask DisposeAsync()
    {
        _patterns.Dispose();
        await Osv.DisposeAsync();
    }
}
