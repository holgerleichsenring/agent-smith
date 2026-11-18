using AgentSmith.Application.Models;
using AgentSmith.Application.Services.Deployment;
using AgentSmith.Application.Services.Handlers;
using AgentSmith.Application.Services.Specs;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Events;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Models;
using AgentSmith.Tests.Handlers;
using AgentSmith.Tests.Specs;
using AgentSmith.Tests.TestHelpers;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AgentSmith.Tests.Deployment;

/// <summary>2026-11-18-3863c: a project whose 'api' context is delivered after a GitOps template to test (no approval,
/// its own repo) and prod (approval, the gitops repo, promoted from test); hosts faked at the provider.</summary>
internal sealed class DeployFixture
{
    public const string Declaration = """
        delivery: gitops_pr
        environments:
          - name: test
            manifest: deploy/test/kustomization.yaml
            version_key: images.0.newTag
          - name: prod
            manifest: deploy/prod/kustomization.yaml
            version_key: images.0.newTag
            promotes_from: test
        """;

    public static string Manifest(string tag) => $"images:\n  - name: app\n    newTag: {tag}  # pinned\n";

    public static readonly RepoConnection AppRepo = new() { Name = "app", Url = "https://stub.test/app" };
    public static readonly RepoConnection GitOps = new() { Name = "gitops", Url = "https://stub.test/gitops" };

    public Mock<ISourceProvider> App { get; } = Provider(Manifest("v1"), "https://stub.test/app/pull/1");
    public Mock<ISourceProvider> Ops { get; } = Provider(Manifest("v0"), "https://stub.test/gitops/pull/2");
    public InMemorySandboxFileReader Template { get; } = new();

    public const string DeclarationPath = "/work/.agentsmith/contexts/k8s-gitops/deployment.yaml";

    public DeployFixture() => Template.Files[DeclarationPath] = Declaration;

    /// <summary>2026-11-18-3863e: whether the hosts answer with <see cref="Runner"/> or run no pipeline.</summary>
    public bool HostRunsPipelines { get; set; } = true;

    /// <summary>2026-11-18-3863f: what the deliveries told the server.</summary>
    public List<RunEvent> Published { get; } = [];

    public static ResolvedProject Project() => new()
    {
        Name = "shop", Repos = [AppRepo],
        Templates =
        [
            new ProjectTemplate("api", "k8s-gitops", "v1.0.0", new RepoConnection { Name = "delivery", Url = "https://stub.test/delivery" },
                null, TemplateKinds.Deployment,
                [
                    new DeploymentEnvironment("test", null, false, new Dictionary<string, string>()),
                    new DeploymentEnvironment("prod", GitOps, true, new Dictionary<string, string>()),
                ]),
        ],
    };

    /// <summary>2026-11-18-3863e: the pipeline runner every repository's host answers with.</summary>
    public Mock<IPipelineRunner> Runner { get; } = new();

    public DeployToEnvironmentHandler Handler()
    {
        var sources = new Mock<ISourceProviderFactory>();
        sources.Setup(f => f.Create(It.Is<RepoConnection>(r => r.Name == "app"))).Returns(App.Object);
        sources.Setup(f => f.Create(It.Is<RepoConnection>(r => r.Name == "gitops"))).Returns(Ops.Object);
        var keys = new ManifestVersionKey();
        var manifests = new DeploymentManifest(sources.Object, keys);
        var scopes = new ProjectTemplateScopes(
            new CodingMasterTemplateTests.FixedScopeFactory(new CodingMasterTemplateTests.RecordingScope()),
            NullLogger<ProjectTemplateScopes>.Instance);
        var events = new Mock<IEventPublisher>();
        events.Setup(e => e.PublishAsync(It.IsAny<RunEvent>(), It.IsAny<CancellationToken>()))
            .Callback<RunEvent, CancellationToken>((e, _) => Published.Add(e)).Returns(Task.CompletedTask);
        var published = new DeployDeliveredPublisher(events.Object, TimeProvider.System);
        var runners = new Mock<IPipelineRunnerFactory>();
        runners.Setup(f => f.Create(It.IsAny<RepoConnection>())).Returns(() => HostRunsPipelines ? Runner.Object : null);
        return new DeployToEnvironmentHandler(
            new DeploymentTemplateRead(scopes, new DerivationTestLooks.FixedReaderFactory(Template),
                new DeploymentDeclarationParser(), NullLogger<DeploymentTemplateRead>.Instance),
            [
                new GitOpsDeployDelivery(new DeployManifestPlan(new DeployVersion(manifests), manifests, keys),
                    new GitOpsDelivery(sources.Object, new DeployCiStatus(NullLogger<DeployCiStatus>.Instance)), published),
                new PipelineDeployDelivery(new DeployPipelinePlan(new DeployVersion(manifests)),
                    new PipelineDelivery(runners.Object, NullLogger<PipelineDelivery>.Instance), published),
            ]);
    }

    public static DeployToEnvironmentContext Context(string environment, string? version)
    {
        var pipeline = new PipelineContext();
        pipeline.Set(ContextKeys.RunId, "20261118-0101-ab12");
        return new DeployToEnvironmentContext(pipeline, Project(), new DeployRequest("api", null, environment, version));
    }

    private static Mock<ISourceProvider> Provider(string manifest, string prUrl)
    {
        var provider = new Mock<ISourceProvider>();
        provider.Setup(p => p.TryReadFileAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(manifest);
        provider.Setup(p => p.WriteFilesToBranchAsync(It.IsAny<BranchName>(), It.IsAny<IReadOnlyList<RepoFile>>(),
            It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(BranchWriteResult.Ok("abc"));
        provider.Setup(p => p.CreatePullRequestAsync(It.IsAny<Repository>(), It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<CancellationToken>(), It.IsAny<PullRequestTicket?>(), It.IsAny<bool>(), It.IsAny<BranchName?>()))
            .ReturnsAsync(prUrl);
        provider.Setup(p => p.MergePullRequestAsync(It.IsAny<string>(), It.IsAny<BranchName>(), It.IsAny<BranchProtection>(),
            It.IsAny<CancellationToken>())).ReturnsAsync(PullRequestCompletion.Merged());
        return provider;
    }
}
