using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Reviews;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Moq;

namespace AgentSmith.PipelineHarness.Composition;

/// <summary>
/// 2026-11-23-933dd: the source repo host's build pipeline and the target's registry, faked for a composition run — a
/// started build ends green with a pushed digest the registry then resolves, or, with <see cref="CriticalInImage"/>,
/// red with a gate failure and a report holding a vulnerability scored 9.8.
/// </summary>
internal sealed class PocHarnessBuildHost
{
    internal const string Digest = "sha256:9330000000000000000000000000000000000000000000000000000000000dd0";

    private string? _tagged;

    internal bool CriticalInImage { get; set; }

    internal List<PipelineRunOrder> Starts { get; } = [];

    internal void Override(IServiceCollection services)
    {
        var runner = new Mock<IPipelineRunner>();
        runner.Setup(r => r.StartAsync(It.IsAny<PipelineRunOrder>(), It.IsAny<CancellationToken>()))
            .Callback<PipelineRunOrder, CancellationToken>((o, _) => Starts.Add(o)).ReturnsAsync(new PipelineRunStart("77", "https://host/runs/77"));
        runner.Setup(r => r.ReadAsync("77", It.IsAny<CiReadRequest>(), It.IsAny<CancellationToken>())).ReturnsAsync(() =>
        {
            if (!CriticalInImage) _tagged = Digest;
            return new PipelineRunState(CriticalInImage ? CiStates.Red : CiStates.Green, "https://host/runs/77",
                CriticalInImage ? [new CiFailure("scan", null, "gate failed")] : []);
        });
        runner.Setup(r => r.ReadArtifactTextAsync("77", It.IsAny<PipelineArtifact>(), It.IsAny<long>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string _, PipelineArtifact a, long _, CancellationToken _) => Text(a.File));
        services.RemoveAll<IPipelineRunnerFactory>().AddSingleton(Mock.Of<IPipelineRunnerFactory>(f => f.Create(It.IsAny<RepoConnection>()) == runner.Object));
        var registry = new Mock<IContainerRegistry>();
        registry.Setup(r => r.Reference(It.IsAny<string>(), It.IsAny<string>()))
            .Returns<string, string>((poc, tag) => new ImageReference("pocregistry.azurecr.io", $"pocs/{poc}", tag));
        registry.Setup(r => r.ResolveAsync(It.IsAny<ImageReference>(), It.IsAny<CancellationToken>())).ReturnsAsync(() => _tagged);
        registry.Setup(r => r.ExistsAsync(It.IsAny<ImageReference>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ImageReference r, CancellationToken _) => r.Digest == _tagged);
        services.RemoveAll<IContainerRegistryFactory>().AddSingleton(Mock.Of<IContainerRegistryFactory>(f => f.Create(It.IsAny<PocRegistryConfig>()) == registry.Object));
    }

    private PipelineArtifactText? Text(string file)
    {
        var text = file switch
        {
            "report.json" => CriticalInImage
                ? """[{"kind":"vulnerability","id":"CVE-2024-933","package":"openssl","installed":"3.0.1","fixed":"3.0.9","severity":"HIGH","scores":[9.8],"title":"t","target":"debian 12"}]"""
                : "[]",
            "gate.txt" => CriticalInImage ? "1\n" : "0\n",
            "digest.txt" => CriticalInImage ? null : Digest,
            _ => null,
        };
        return text is null ? null : PipelineArtifactText.Of(text, text.Length);
    }
}
