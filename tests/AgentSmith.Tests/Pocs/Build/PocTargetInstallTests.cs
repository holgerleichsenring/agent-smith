using AgentSmith.Application.Models;
using AgentSmith.Application.Services.Handlers;
using AgentSmith.Application.Services.Pocs;
using AgentSmith.Application.Services.Pocs.Argo;
using AgentSmith.Application.Services.Pocs.GitOps;
using AgentSmith.Application.Services.Pocs.Pipeline;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Models;
using AgentSmith.Server.Services.Acts;
using AgentSmith.Server.Services.Pocs.Targets;
using AgentSmith.Server.Services.Tickets;
using AgentSmith.Tests.Pocs.Argo;
using FluentAssertions;
using Moq;

namespace AgentSmith.Tests.Pocs.Build;

/// <summary>2026-11-23-933dd: the poc-target-install run writes the build pipeline once, refuses a foreign file at its
/// path, and the Install action starts it as a run on the installation.</summary>
public sealed class PocTargetInstallTests
{
    private readonly Mock<ISourceProvider> _provider = new();
    private readonly List<IReadOnlyList<RepoFile>> _writes = [];
    private readonly Dictionary<string, string> _held = new(StringComparer.Ordinal);

    /// <summary>2026-11-23-c81ec: the target's Argo CD the install upserts the ApplicationSet on.</summary>
    internal FakeArgoCd Argo { get; } = new();

    private IArgoCdClient? _argoOverride;

    public PocTargetInstallTests()
    {
        _provider.Setup(p => p.GetDefaultBranchAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new BranchName("main"));
        _provider.Setup(p => p.TryReadFileAsync(It.IsAny<string>(), It.IsAny<BranchName>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string path, BranchName _, CancellationToken _) => _held.GetValueOrDefault(path));
        _provider.Setup(p => p.ListFilesUnderAsync(It.IsAny<string>(), It.IsAny<BranchName>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string folder, BranchName _, int _, CancellationToken _) =>
                new RepoFileListing(_held.Keys.Where(k => k.StartsWith(folder + "/", StringComparison.Ordinal)).ToList(), false));
        _provider.Setup(p => p.WriteFilesToBranchAsync(It.IsAny<BranchName>(), It.IsAny<IReadOnlyList<RepoFile>>(), It.IsAny<string>(), It.IsAny<CancellationToken>(), It.IsAny<BranchName?>()))
            .Callback<BranchName, IReadOnlyList<RepoFile>, string, CancellationToken, BranchName?>((_, files, _, _, _) =>
            {
                _writes.Add(files);
                foreach (var file in files) _held[file.Path] = file.Content;
            })
            .ReturnsAsync(BranchWriteResult.Ok("abc123"));
    }

    [Fact]
    public async Task Execute_WritesTheDefinitionOnce_ASecondRunWritesNothingAndSaysSo()
    {
        var first = await Handler().ExecuteAsync(new PocTargetInstallContext(new PipelineContext(), "main"), default);
        var second = await Handler().ExecuteAsync(new PocTargetInstallContext(new PipelineContext(), "main"), default);

        first.IsSuccess.Should().BeTrue(first.Message);
        first.Message.Should().Contain("commit abc123").And.Contain(".github/workflows/poc-build.yml");
        _writes[0].Single().Path.Should().Be(".github/workflows/poc-build.yml");
        _writes.Should().HaveCount(2, "the pipeline, then the app template");
        second.Message.Should().Contain("nothing was written");
        second.Message.Should().Contain("already holds the app template");
    }

    // 2026-11-23-c81eb: the six default template files are written once, under <template_path>/app/.
    [Fact]
    public async Task Execute_WritesTheSixTemplateFilesOnce()
    {
        await Handler().ExecuteAsync(new PocTargetInstallContext(new PipelineContext(), "main"), default);
        await Handler().ExecuteAsync(new PocTargetInstallContext(new PipelineContext(), "main"), default);

        _writes.Should().HaveCount(2);
        _writes[1].Select(f => f.Path).Should().BeEquivalentTo(
            new[] { "namespace", "resourcequota", "deployment", "service", "ingress", "networkpolicy" }.Select(n => $"poc-template/app/{n}.yaml"));
        _held["poc-template/app/resourcequota.yaml"].Should().Contain("limits.memory: \"1Gi\"").And.NotContain("@@");
        _held["poc-template/app/networkpolicy.yaml"].Should().Contain("kubernetes.io/metadata.name: \"ingress-nginx\"")
            .And.Contain("podSelector:").And.Contain("{\"app\": \"egress-proxy\"}");
    }

    [Fact]
    public async Task Execute_AnOperatorEditedTemplateFile_IsNeverOverwritten()
    {
        _held["poc-template/app/deployment.yaml"] = "# mine\n";

        var result = await Handler().ExecuteAsync(new PocTargetInstallContext(new PipelineContext(), "main"), default);

        result.IsSuccess.Should().BeTrue(result.Message);
        _held["poc-template/app/deployment.yaml"].Should().Be("# mine\n");
        _writes[1].Should().HaveCount(5).And.NotContain(f => f.Path.EndsWith("deployment.yaml"));
    }

    [Fact]
    public async Task Execute_AFileAgentSmithDidNotWrite_IsRefusedNamingIt()
    {
        _held[".github/workflows/poc-build.yml"] = "name: ci\n";

        var result = await Handler().ExecuteAsync(new PocTargetInstallContext(new PipelineContext(), "main"), default);

        result.IsSuccess.Should().BeFalse();
        result.Message.Should().Contain(".github/workflows/poc-build.yml").And.Contain("did not write");
        _writes.Should().BeEmpty();
    }

    // 2026-11-23-c81ec: the ApplicationSet is created on the first install; a stored one with server-added defaults but
    // an equal subset writes nothing, a changed one is upserted.
    [Fact]
    public async Task Execute_TheApplicationSet_IsCreatedThenUnchangedDespiteServerDefaults_ThenUpdatedWhenChanged()
    {
        var first = await Handler().ExecuteAsync(new PocTargetInstallContext(new PipelineContext(), "main"), default);
        Argo.StoredSet!["status"] = new System.Text.Json.Nodes.JsonObject { ["conditions"] = new System.Text.Json.Nodes.JsonArray() };
        Argo.StoredSet["metadata"]!["uid"] = "u-1";
        ((System.Text.Json.Nodes.JsonObject)Argo.StoredSet["spec"]!["template"]!["spec"]!)["revisionHistoryLimit"] = 10;
        var second = await Handler().ExecuteAsync(new PocTargetInstallContext(new PipelineContext(), "main"), default);
        Argo.StoredSet["spec"]!["generators"]![0]!["git"]!["requeueAfterSeconds"] = 180;
        var third = await Handler().ExecuteAsync(new PocTargetInstallContext(new PipelineContext(), "main"), default);

        first.Message.Should().Contain("The ApplicationSet pocs-main on Argo CD https://argocd.poc.example.internal is created.");
        second.Message.Should().Contain("is unchanged");
        third.Message.Should().Contain("is updated");
        Argo.Upserts.Should().HaveCount(2);
        Argo.Upserts[1]["spec"]!["generators"]![0]!["git"]!["requeueAfterSeconds"]!.GetValue<int>().Should().Be(30);
    }

    [Fact]
    public async Task Execute_ArgoRefusesTheToken_FailsNamingIt()
    {
        Argo.StoredSet = null;
        var refusing = new Mock<IArgoCdClient>();
        refusing.Setup(c => c.GetApplicationSetAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new AgentSmith.Contracts.Exceptions.ArgoCdException("Argo CD https://argocd.poc.example.internal answered status 403 for api/v1/applicationsets/pocs-main"));
        _argoOverride = refusing.Object;

        var result = await Handler().ExecuteAsync(new PocTargetInstallContext(new PipelineContext(), "main"), default);

        result.IsSuccess.Should().BeFalse();
        result.Message.Should().Contain("The ApplicationSet pocs-main was not installed").And.Contain("403");
    }

    [Fact]
    public async Task Execute_AnUnknownTarget_Fails() =>
        (await Handler().ExecuteAsync(new PocTargetInstallContext(new PipelineContext(), "other"), default)).IsSuccess.Should().BeFalse();

    [Fact]
    public async Task Install_StartsTheRunOnTheInstallation_AndAnswersItsOutcome()
    {
        var runs = new Mock<IActRun>();
        runs.Setup(r => r.RunAsync(InstallationScope.Name, PipelinePresets.PocTargetInstallName, It.IsAny<IReadOnlyDictionary<string, object>>(),
                null, It.IsAny<string>())).ReturnsAsync(CommandResult.Ok("The build pipeline of target 'main' is commit abc123."));
        var launcher = new PocTargetInstallLauncher(runs.Object, new TicketProjects(Loader(), new ServerContext("c.yml")), TimeProvider.System);

        var answer = await launcher.InstallAsync("MAIN", default);

        answer!.Succeeded.Should().BeTrue();
        answer.Message.Should().Contain("abc123");
        answer.RunId.Should().NotBeNullOrEmpty();
        runs.Verify(r => r.RunAsync(InstallationScope.Name, PipelinePresets.PocTargetInstallName,
            It.Is<IReadOnlyDictionary<string, object>>(s => (string)s[ContextKeys.PocTargetName] == "main"), null, answer.RunId));
        (await launcher.InstallAsync("other", default)).Should().BeNull();
    }

    private PocTargetInstallHandler Handler()
    {
        var sources = Mock.Of<ISourceProviderFactory>(f => f.Create(It.IsAny<RepoConnection>()) == _provider.Object);
        return new PocTargetInstallHandler(new PocsConfigSource(Loader(), new ServerContext("c.yml")),
            new PocSourceRepos(Mock.Of<IConnectionRepoUrlBuilder>()), PocPipelineTemplateTests.Template(), new PocPipelineWrite(sources),
            new PocAppTemplateWrite(sources, new PocAppTemplateDefaults(new PocYamlFill())),
            new PocApplicationSetInstall(_argoOverride is null ? Argo : Mock.Of<IArgoCdClientFactory>(f => f.Create(It.IsAny<PocArgoConfig>()) == _argoOverride), sources, new PocApplicationSet(), new PocApplicationSetMatch(),
                Microsoft.Extensions.Logging.Abstractions.NullLogger<PocApplicationSetInstall>.Instance));
    }

    private static IConfigurationLoader Loader()
    {
        var pocs = PocsConfigTests.Sound();
        pocs.Targets[0].SourceRepo = "poc-src";
        var config = new AgentSmithConfig
        {
            Pocs = pocs,
            Repos = new() { ["poc-src"] = new RepoConnection { Name = "poc-src", Type = RepoType.GitHub, Url = "https://github.com/example/poc-src" } },
        };
        return Mock.Of<IConfigurationLoader>(l => l.LoadConfig(It.IsAny<string>()) == config);
    }
}
