using AgentSmith.Application.Models.Sandbox;
using AgentSmith.Application.Services.Sandbox;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Runs;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Models;
using AgentSmith.Infrastructure.Services;
using AgentSmith.Server.Services.Preconditions;
using AgentSmith.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AgentSmith.Tests.Preconditions;

/// <summary>
/// 2026-11-16-f3fbc: one pass over the stuck tickets nudges PreconditionsMet only for those whose precondition holds
/// now — an image the live boundary accepts, repositories whose base reads Complete (each probed once per pass), a
/// config check on the epoch's first pass only.
/// </summary>
public sealed class PreconditionReleasePassTests
{
    private const string Image = "sbtscala/scala-sbt:1.11";
    private readonly List<StuckOnPrecondition> _stuck = [];
    private readonly List<ReworkNudgeRequest> _nudged = [];
    private readonly Dictionary<string, Mock<ISourceProvider>> _repos = new(StringComparer.Ordinal);
    private SandboxGlobalConfig _sandbox = new();

    private static readonly AgentSmithConfig Config = new()
    {
        Projects = new Dictionary<string, ResolvedProject>
        {
            ["p"] = new() { Name = "p", Repos = [Repo("api"), Repo("web")] },
        },
    };

    private static RepoConnection Repo(string name) =>
        new() { Name = name, Url = $"https://github.com/o/{name}", Type = RepoType.GitHub, DefaultBranch = "main" };

    private sealed class FixedTrust(Func<SandboxGlobalConfig> sandbox) : IImageRegistryTrust
    {
        public RegistryTrustBoundary InForce() => RegistryTrustBoundary.From(sandbox());
    }

    private void Stuck(string ticket, string kind, EndDetail? detail) => _stuck.Add(new("p", ticket, $"run-{ticket}", kind, detail));

    private static EndDetail ImageDetail() => new(ContextImageDetail(), ["api"]);

    private static string ContextImageDetail() =>
        new ContextImageBoundary(new FixedTrust(() => new SandboxGlobalConfig())).Detail(Image, "default", "api").Subject;

    private static EndDetail Bootstrap(params string[] repos) => new("p", repos, "Initialise", "p", "/config/projects");

    // A repository whose main branch carries a context, with or without its principles.
    private Mock<ISourceProvider> Repo(string name, bool complete)
    {
        var provider = new Mock<ISourceProvider>();
        provider.Setup(p => p.BranchExistsAsync(It.IsAny<BranchName>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);
        provider.Setup(p => p.ListDirectoryAsync(It.IsAny<string>(), It.IsAny<BranchName>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(["api"]);
        provider.Setup(p => p.ListDirectoryAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync([]);
        provider.Setup(p => p.TryReadFileAsync(It.IsAny<string>(), It.IsAny<BranchName>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string path, BranchName _, CancellationToken _) => path switch
            {
                ".agentsmith/contexts/api/context.yaml" => "meta:\n  project: api\n  workdir: .\nstack:\n  lang: csharp\n",
                ".agentsmith/contexts/api/principles.md" when complete => "# principles",
                _ => null,
            });
        _repos[name] = provider;
        return provider;
    }

    private Task<int> PassAsync(bool epochStart)
    {
        var stuck = new Mock<IStuckOnPreconditionReader>();
        stuck.Setup(s => s.ListAsync(It.IsAny<CancellationToken>())).ReturnsAsync(() => [.. _stuck]);
        var nudges = new Mock<IReworkNudges>();
        nudges.Setup(n => n.EnqueueAsync(It.IsAny<ReworkNudgeRequest>(), It.IsAny<CancellationToken>()))
            .Callback<ReworkNudgeRequest, CancellationToken>((r, _) => _nudged.Add(r)).Returns(Task.CompletedTask);
        var sources = new Mock<ISourceProviderFactory>();
        sources.Setup(f => f.Create(It.IsAny<RepoConnection>()))
            .Returns((RepoConnection r) => _repos.TryGetValue(r.Name, out var p) ? p.Object : throw new InvalidOperationException("unreachable"));
        var resolver = TestSandboxLanguageResolver.Create(sources.Object, new ContextYamlParser(new ContextYamlSerializer(new ContextYamlBuilders())));
        var readiness = new RepoReadiness(new RemoteBootstrapProbe(sources.Object, resolver, NullLogger<RemoteBootstrapProbe>.Instance),
            sources.Object, NullLogger<RepoReadiness>.Instance);
        var check = new PreconditionHoldCheck(new ContextImageBoundary(new FixedTrust(() => _sandbox)));
        return new PreconditionReleasePass(stuck.Object, check, readiness, nudges.Object, NullLogger<PreconditionReleasePass>.Instance)
            .RunAsync(Config, epochStart, CancellationToken.None);
    }

    private IEnumerable<string> Nudged() => _nudged.Select(n => n.TicketId);

    [Fact]
    public async Task Pass_ImageNowTrusted_Nudges()
    {
        Stuck("1", RunEndKinds.ImageUntrusted, ImageDetail());
        _sandbox = new SandboxGlobalConfig { AllowedRegistries = ["sbtscala/"] };

        (await PassAsync(epochStart: false)).Should().Be(1);

        _nudged.Should().ContainSingle().Which.Should().Be(new ReworkNudgeRequest("p", "1", ReworkNudgeOrigin.PreconditionsMet));
    }

    [Fact]
    public async Task Pass_ImageStillUntrusted_DoesNotNudge()
    {
        Stuck("1", RunEndKinds.ImageUntrusted, ImageDetail());

        (await PassAsync(epochStart: true)).Should().Be(0);
    }

    [Fact]
    public async Task Pass_ReposReady_Nudges()
    {
        Repo("api", complete: true);
        Repo("web", complete: true);
        Stuck("2", RunEndKinds.BootstrapMissing, Bootstrap("api", "web"));

        await PassAsync(epochStart: false);

        Nudged().Should().Equal("2");
    }

    [Fact]
    public async Task Pass_OneRepoIncompleteOrUnknown_DoesNotNudge()
    {
        Repo("api", complete: true);
        Repo("web", complete: false);
        Stuck("2", RunEndKinds.BootstrapMissing, Bootstrap("api", "web"));
        Stuck("3", RunEndKinds.BootstrapMissing, Bootstrap("api", "gone"));
        Stuck("4", RunEndKinds.BootstrapMissing, new EndDetail("p", []));

        await PassAsync(epochStart: false);

        Nudged().Should().BeEmpty("an incomplete, unconfigured or unnamed repository is never proof the init landed");
    }

    [Fact]
    public async Task Pass_UnreachableHost_DoesNotNudge()
    {
        Stuck("2", RunEndKinds.BootstrapMissing, Bootstrap("api"));

        await PassAsync(epochStart: false);

        Nudged().Should().BeEmpty("an unknown answer is not Complete");
    }

    [Fact]
    public async Task Pass_Preflight_NudgedOnlyOnTheEpochStart()
    {
        Stuck("5", RunEndKinds.Preflight, new EndDetail("agents.a.model", []));

        await PassAsync(epochStart: false);
        Nudged().Should().BeEmpty("only a saved config changes a config check");
        await PassAsync(epochStart: true);
        Nudged().Should().Equal("5");
    }

    [Fact]
    public async Task Pass_RepoNamedByTwoTickets_ProbedOnce()
    {
        var api = Repo("api", complete: true);
        Stuck("2", RunEndKinds.BootstrapMissing, Bootstrap("api"));
        Stuck("3", RunEndKinds.BootstrapMissing, Bootstrap("api"));

        await PassAsync(epochStart: false);

        Nudged().Should().Equal("2", "3");
        api.Verify(p => p.TryReadFileAsync(".agentsmith/contexts/api/principles.md", It.IsAny<BranchName>(), It.IsAny<CancellationToken>()),
            Times.Once, "one probe per repository per pass");
    }

    [Fact]
    public async Task Pass_ProjectNoLongerConfigured_Skipped()
    {
        _stuck.Add(new("gone", "9", "run-9", RunEndKinds.Preflight, null));

        (await PassAsync(epochStart: true)).Should().Be(0);
    }
}
