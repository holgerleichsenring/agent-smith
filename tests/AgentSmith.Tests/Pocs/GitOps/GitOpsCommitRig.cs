using AgentSmith.Application.Models;
using AgentSmith.Application.Services.Deployment;
using AgentSmith.Application.Services.Handlers;
using AgentSmith.Application.Services.Pocs;
using AgentSmith.Application.Services.Pocs.GitOps;
using AgentSmith.Application.Services.Pocs.Pipeline;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Events;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Pocs;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Models;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AgentSmith.Tests.Pocs.GitOps;

/// <summary>2026-11-23-c81eb: GitOpsCommitHandler over the real render and commit, an in-memory branch holding the
/// default template, an in-memory row and recorded events.</summary>
internal sealed class GitOpsCommitRig
{
    public FakeGitOpsBranch Branch { get; } = new();

    public FakeGitOpsStore Store { get; } = new();

    public List<PocDeployedEvent> Published { get; } = [];

    public List<PocActedEvent> Acted { get; } = [];

    public PocsConfig Pocs { get; } = PocsConfigTests.Sound();

    public GitOpsCommitRig()
    {
        foreach (var file in new PocAppTemplateDefaults(new PocYamlFill()).Render(Pocs.Targets[0]))
            Branch.Files[$"poc-template/app/{file.Name}"] = file.Text;
    }

    public GitOpsCommitHandler Handler()
    {
        var events = new Mock<IEventPublisher>();
        events.Setup(e => e.PublishAsync(It.IsAny<RunEvent>(), It.IsAny<CancellationToken>()))
            .Callback<RunEvent, CancellationToken>((e, _) => Record(e)).Returns(Task.CompletedTask);
        var config = new AgentSmithConfig { Pocs = Pocs };
        var source = new PocsConfigSource(Mock.Of<IConfigurationLoader>(l => l.LoadConfig(It.IsAny<string>()) == config), new ServerContext("c.yml"));
        var factory = Branch.Factory();
        var planner = new PocGitOpsPlanner(new PocTemplateRead(), PocRenderRig.Render(), new PocFolderPlan());
        var delivery = new GitOpsDelivery(factory, new DeployCiStatus(NullLogger<DeployCiStatus>.Instance));
        var acted = new PocActedPublisher(events.Object, TimeProvider.System);
        var commit = new PocGitOpsCommit(factory, planner, delivery, Store, new PocDeployedPublisher(events.Object, TimeProvider.System), acted);
        var delete = new PocGitOpsDelete(factory, new PocAbsentPlan(), delivery, Store, acted); // 2026-11-23-c81ed
        return new GitOpsCommitHandler(Store, source, new PocDesiredStates(Store), new PocRenderInputs(Store, Registries()), commit, delete,
            NullLogger<GitOpsCommitHandler>.Instance);
    }

    private void Record(RunEvent e)
    {
        if (e is PocDeployedEvent deployed) Published.Add(deployed);
        if (e is PocActedEvent act) Acted.Add(act);
    }

    public GitOpsCommitContext Context(PocSealResult? seal, string runId = "run-1")
    {
        var pipeline = new PipelineContext();
        pipeline.Set(ContextKeys.RunId, runId);
        pipeline.Set(ContextKeys.PocId, "7");
        pipeline.Set(ContextKeys.PocRevision, "2");
        pipeline.Set(ContextKeys.PocActor, "ann");
        pipeline.Set(ContextKeys.PocImageDigest, PocRenderRig.Digest);
        if (seal is not null) pipeline.Set(ContextKeys.PocSeal, seal);
        var project = new ResolvedProject { Name = "poc-ann", Repos = [new RepoConnection { Name = "poc-src", Type = RepoType.GitHub, Url = "https://github.com/example/poc-src" }] };
        return new GitOpsCommitContext(pipeline, project, PocSeeds.From(pipeline));
    }

    public Task<CommandResult> RunAsync(PocSealResult? seal, string runId = "run-1") => Handler().ExecuteAsync(Context(seal, runId), default);

    private static IContainerRegistryFactory Registries()
    {
        var registry = new Mock<IContainerRegistry>();
        registry.Setup(r => r.Reference(It.IsAny<string>(), It.IsAny<string>()))
            .Returns((string poc, string tag) => new ImageReference("pocregistry.azurecr.io", $"pocs/{poc}", tag));
        return Mock.Of<IContainerRegistryFactory>(f => f.Create(It.IsAny<PocRegistryConfig>()) == registry.Object);
    }
}
