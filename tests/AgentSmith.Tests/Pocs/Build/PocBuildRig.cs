using AgentSmith.Application.Models;
using AgentSmith.Application.Services.Handlers;
using AgentSmith.Application.Services.Pocs;
using AgentSmith.Application.Services.Pocs.Build;
using AgentSmith.Application.Services.Pocs.Scan;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Events;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Pocs;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Reviews;
using AgentSmith.Contracts.Services;
using AgentSmith.Tests.Pocs.Scan;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AgentSmith.Tests.Pocs.Build;

/// <summary>
/// 2026-11-23-933dd: the build step with its real parts — the rules, the report reader, the start, the follow — over a
/// faked host runner, a faked registry and the in-memory store. Revision 1 of 'todo' is validated and committed at
/// <see cref="Sha"/>; the catalog's secret confidences are the shipped ones for the mapped patterns.
/// </summary>
internal sealed class PocBuildRig
{
    internal const string Sha = "0123456789abcdef0123456789abcdef01234567";
    internal const string Digest = "sha256:1111111111111111111111111111111111111111111111111111111111111111";

    internal FakeScanStore Store { get; } = new();
    internal Mock<IPipelineRunner> Runner { get; } = new();
    internal Mock<IContainerRegistry> Registry { get; } = new();
    internal List<PipelineRunOrder> Starts { get; } = [];
    internal List<RunEvent> Events { get; } = [];
    internal SkippingClock Clock { get; } = new();
    internal PocsConfig Pocs { get; } = PocsConfigTests.Sound();
    internal RepoType Host { get; set; } = RepoType.GitHub;
    internal Dictionary<string, string?> Artifacts { get; } = [];
    internal string? Tagged { get; set; }

    internal PocBuildRig()
    {
        Store.Rows[1] = new FakeScanStore.Row { State = PocRevisionStates.Validated, VerdictJson = new PocScanVerdict([], []).ToJson(), CommitSha = Sha };
        Registry.Setup(r => r.Reference(It.IsAny<string>(), It.IsAny<string>()))
            .Returns<string, string>((poc, tag) => new ImageReference("pocregistry.azurecr.io", $"pocs/{poc}", tag));
        Registry.Setup(r => r.ResolveAsync(It.IsAny<ImageReference>(), It.IsAny<CancellationToken>())).ReturnsAsync(() => Tagged);
        Registry.Setup(r => r.ExistsAsync(It.IsAny<ImageReference>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ImageReference r, CancellationToken _) => r.Digest == Tagged);
        Runner.Setup(r => r.StartAsync(It.IsAny<PipelineRunOrder>(), It.IsAny<CancellationToken>()))
            .Callback<PipelineRunOrder, CancellationToken>((o, _) => Starts.Add(o))
            .ReturnsAsync(() => new PipelineRunStart($"run-{Starts.Count}", $"https://host/runs/{Starts.Count}"));
        Runner.Setup(r => r.ReadArtifactTextAsync(It.IsAny<string>(), It.IsAny<PipelineArtifact>(), It.IsAny<long>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string run, PipelineArtifact a, long max, CancellationToken _) =>
                Artifacts.TryGetValue($"{run}/{a.File}", out var text) && text is not null
                    ? text.Length > max ? PipelineArtifactText.TooLarge(text.Length) : PipelineArtifactText.Of(text, text.Length)
                    : null);
    }

    /// <summary>The run's states in order; the last one repeats.</summary>
    internal void States(string runId, params string[] states)
    {
        var queue = new Queue<string>(states);
        Runner.Setup(r => r.ReadAsync(runId, It.IsAny<CiReadRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => new PipelineRunState(queue.Count > 1 ? queue.Dequeue() : queue.Peek(), $"https://host/{runId}",
                [new CiFailure("push", "https://host/job", "registry answered 503")]));
    }

    /// <summary>A report of the given entries (JSON objects) and a gate status for <paramref name="runId"/>.</summary>
    internal void Report(string runId, string gate, params string[] entries)
    {
        Artifacts[$"{runId}/report.json"] = "[" + string.Join(',', entries) + "]";
        Artifacts[$"{runId}/gate.txt"] = gate + "\n";
    }

    internal static string Vulnerability(string id, double score, string? fixedVersion = "1.1", string severity = "LOW") =>
        $$"""{"kind":"vulnerability","id":"{{id}}","package":"openssl","installed":"1.0","fixed":"{{fixedVersion}}","severity":"{{severity}}","scores":[{{score.ToString(System.Globalization.CultureInfo.InvariantCulture)}}],"title":"t","target":"debian 12"}""";

    internal static string Secret(string rule, string severity = "HIGH") =>
        $$"""{"kind":"secret","id":"{{rule}}","severity":"{{severity}}","title":"key","target":"/app/key.pem","line":1}""";

    internal PocPipelineBuildHandler Handler()
    {
        var ids = new PocFindingIds();
        var confidences = new Mock<IPocSecretConfidences>();
        confidences.Setup(c => c.Read()).Returns((Confidences(), null));
        var secretRules = new PocTrivySecretRules(confidences.Object);
        var ignore = new PocTrivyIgnore();
        var registries = Mock.Of<IContainerRegistryFactory>(f => f.Create(It.IsAny<PocRegistryConfig>()) == Registry.Object);
        var runners = Mock.Of<IPipelineRunnerFactory>(f => f.Create(It.IsAny<RepoConnection>()) == Runner.Object);
        var events = new Mock<IEventPublisher>();
        events.Setup(e => e.PublishAsync(It.IsAny<RunEvent>(), It.IsAny<CancellationToken>()))
            .Callback<RunEvent, CancellationToken>((e, _) => Events.Add(e)).Returns(Task.CompletedTask);
        var report = new PocBuildReport(new TrivyReportParser(), new PocImageRules(new PocVulnerabilityRule(ids), new PocSecretRule(ids), ids), secretRules, ignore);
        var registry = new PocBuildRegistry(registries);
        var overrule = new PocBuildOverrule(Store, Store, new PocOverruledPublisher(events.Object, Clock));
        var run = new PocBuildRun(registry, runners,
            new PocBuildStart(new PocBuildInputs(), ignore, secretRules, Store, NullLogger<PocBuildStart>.Instance),
            new PocBuildFollow(Clock), new PocBuildOutcome(report, registry, Store, overrule), Store);
        var config = new AgentSmithConfig { Pocs = Pocs };
        var source = new PocsConfigSource(Mock.Of<IConfigurationLoader>(l => l.LoadConfig(It.IsAny<string>()) == config), new ServerContext("agentsmith.yml"));
        return new PocPipelineBuildHandler(Store, source, overrule, run, NullLogger<PocPipelineBuildHandler>.Instance);
    }

    internal PocPipelineBuildContext Context(bool overrule = false, params string[] ids)
    {
        var pipeline = new PipelineContext();
        pipeline.Set(AgentSmith.Contracts.Commands.ContextKeys.RunId, "as-run");
        return new PocPipelineBuildContext(pipeline,
            new ResolvedProject { Name = "poc-ann", Repos = [new RepoConnection { Name = "poc-src", Type = Host, Url = "https://github.com/example/poc-src" }] },
            new PocSeeds(7, 1, "ann-sub", overrule, overrule ? "a reason" : null, ids));
    }

    internal static IReadOnlyDictionary<string, int> Confidences() => new Dictionary<string, int>
    {
        ["aws-access-key"] = 9, ["aws-secret-key"] = 9, ["rsa-private-key"] = 9, ["stripe-secret-key"] = 9,
        ["stripe-publishable-key"] = 7, ["google-api-key"] = 8, ["jwt-token"] = 7, ["slack-token"] = 9,
    };
}
