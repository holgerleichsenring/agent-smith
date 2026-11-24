using AgentSmith.Application.Models;
using AgentSmith.Application.Services.Handlers;
using AgentSmith.Application.Services.Pocs;
using AgentSmith.Application.Services.Pocs.Acts;
using AgentSmith.Application.Services.Pocs.Lifetime;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Pocs;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Models;
using AgentSmith.Tests.Pocs.GitOps;
using AgentSmith.Tests.Pocs.Lifetime;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AgentSmith.Tests.Pocs.Acts;

/// <summary>2026-11-23-c81ed: the PocAct step followed by the real GitOps commit over the in-memory branch and row — a
/// deployed PoC at revision 2 with an older healthy revision 1, and a registry that has (or lost) revision 1's image.</summary>
internal sealed class PocActRig
{
    public const string OlderDigest = PocActPlanTests.Older;

    public GitOpsCommitRig Commit { get; } = new();

    public FakeActStore Acts { get; }

    public bool ImageExists { get; set; } = true;

    /// <summary>2026-11-23-c81ee: what the expiry re-check reads.</summary>
    public FakeLifetimeStore Lifetimes { get; } = new();

    public PocActRig()
    {
        Acts = new FakeActStore(Commit.Store);
        Commit.Store.Revisions[1] = new PocRenderRevision(1, 8080, "old", OlderDigest);
        Acts.Revisions[1] = new PocActRevision(1, OlderDigest, "old", PocRolloutStates.Healthy);
        Acts.Revisions[2] = new PocActRevision(2, PocRenderRig.Digest, "abc", PocRolloutStates.Healthy);
    }

    /// <summary>Deploys revision 2 with one replica the way poc-deploy does, so the branch holds its folder.</summary>
    public async Task DeployAsync() => await Commit.RunAsync(PocRenderRig.Seal());

    public async Task<(CommandResult Act, CommandResult? Commit, PipelineContext Pipeline)> RunAsync(
        string preset, int? revision = null, string? readiness = null, string runId = "act-1", string? lifetimeCase = null)
    {
        var context = Commit.Context(PocRenderRig.Seal(unchanged: true), runId);
        if (lifetimeCase is not null) context.Pipeline.Set(ContextKeys.PocLifetimeCase, lifetimeCase);
        context.Pipeline.Set(ContextKeys.PipelineName, preset);
        if (revision is { } n) context.Pipeline.Set(ContextKeys.PocRevision, n.ToString(System.Globalization.CultureInfo.InvariantCulture));
        else context.Pipeline.Remove(ContextKeys.PocRevision);
        context.Pipeline.Remove(ContextKeys.PocImageDigest);
        if (readiness is not null) context.Pipeline.Set(ContextKeys.PocReadiness, readiness);
        if (preset == PipelinePresets.PocDeleteName) context.Pipeline.Remove(ContextKeys.PocSeal);
        var act = await ActHandler().ExecuteAsync(new PocActContext(context.Pipeline, context.Project, preset, PocSeeds.From(context.Pipeline)), default);
        if (!act.IsSuccess) return (act, null, context.Pipeline);
        var commit = await Commit.Handler().ExecuteAsync(context with { Seeds = PocSeeds.From(context.Pipeline) }, default);
        return (act, commit, context.Pipeline);
    }

    private PocActHandler ActHandler()
    {
        var config = new AgentSmithConfig { Pocs = Commit.Pocs };
        var source = new PocsConfigSource(Mock.Of<IConfigurationLoader>(l => l.LoadConfig(It.IsAny<string>()) == config), new ServerContext("c.yml"));
        var registry = new Mock<IContainerRegistry>();
        registry.Setup(r => r.Reference(It.IsAny<string>(), It.IsAny<string>()))
            .Returns((string poc, string tag) => new ImageReference("pocregistry.azurecr.io", $"pocs/{poc}", tag));
        registry.Setup(r => r.ExistsAsync(It.IsAny<ImageReference>(), It.IsAny<CancellationToken>())).ReturnsAsync(() => ImageExists);
        var registries = Mock.Of<IContainerRegistryFactory>(f => f.Create(It.IsAny<PocRegistryConfig>()) == registry.Object);
        var lifetime = new PocLifetime();
        var recheck = new PocLifetimeRecheck(Lifetimes, lifetime, new PocLifetimePlan(), new PocLifetimeReasons(), Commit.Clock);
        return new PocActHandler(Acts, source, new PocActPlan(), new PocRollbackImage(registries), lifetime, recheck, Commit.Clock,
            NullLogger<PocActHandler>.Instance);
    }
}
