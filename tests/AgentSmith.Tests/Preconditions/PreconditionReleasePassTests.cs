using AgentSmith.Application.Models.Sandbox;
using AgentSmith.Application.Services;
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
/// now — an image the live boundary accepts, repositories that read Complete by the run's own rule on the branch and
/// parent the attempt recorded (each probed once per pass), a config check once the stored config changed after the
/// attempt ended. An attempt that stops again on the same evidence is not released again until it changes.
/// </summary>
public sealed class PreconditionReleasePassTests
{
    private const string Image = "sbtscala/scala-sbt:1.11";
    private readonly List<StuckOnPrecondition> _stuck = [];
    private readonly List<ReworkNudgeRequest> _nudged = [];
    private readonly Dictionary<string, Mock<ISourceProvider>> _repos = new(StringComparer.Ordinal);
    private SandboxGlobalConfig _sandbox = new();
    private DateTimeOffset? _configChangedAt;
    private static readonly DateTimeOffset T0 = new(2026, 11, 16, 9, 0, 0, TimeSpan.Zero);

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

    private void Stuck(string ticket, string kind, EndDetail? detail, DateTimeOffset? finishedAt = null) =>
        _stuck.Add(new("p", ticket, $"run-{ticket}", kind, detail, finishedAt ?? T0));

    private static EndDetail ImageDetail() => new(ContextImageDetail(), ["api"]);

    private static string ContextImageDetail() =>
        new ContextImageBoundary(new FixedTrust(() => new SandboxGlobalConfig())).Detail(Image, "default", "api").Subject;

    private static EndDetail Bootstrap(params string[] repos) => new("p", repos, "Initialise", "p", "/config/projects");

    private static EndDetail SliceBootstrap(string branch, string parent) =>
        new("p", ["api"], "Initialise", "p", "/config/projects", branch, parent);

    private static string Rung(string parent) => TicketBranchNamer.Compose(new TicketId(parent)).Value;

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

    // A repository whose listed branches exist and carry their bootstrap as given; any other branch is absent.
    private void RepoOnBranches(string name, Dictionary<string, bool> branches)
    {
        var provider = Repo(name, complete: false);
        provider.Setup(p => p.BranchExistsAsync(It.IsAny<BranchName>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((BranchName b, CancellationToken _) => branches.ContainsKey(b.Value));
        provider.Setup(p => p.TryReadFileAsync(It.IsAny<string>(), It.IsAny<BranchName>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string path, BranchName b, CancellationToken _) => path switch
            {
                ".agentsmith/contexts/api/context.yaml" when branches.ContainsKey(b.Value) => "meta:\n  project: api\n  workdir: .\nstack:\n  lang: csharp\n",
                ".agentsmith/contexts/api/principles.md" when branches.GetValueOrDefault(b.Value) => "# principles",
                _ => null,
            });
    }

    private Task<int> PassAsync()
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
        var clock = new Mock<IConfigChangeClock>();
        clock.Setup(c => c.LastChangedAtAsync(It.IsAny<CancellationToken>())).ReturnsAsync(() => _configChangedAt);
        return new PreconditionReleasePass(stuck.Object, check, readiness, clock.Object, nudges.Object,
            NullLogger<PreconditionReleasePass>.Instance).RunAsync(Config, CancellationToken.None);
    }

    private IEnumerable<string> Nudged() => _nudged.Select(n => n.TicketId);

    [Fact]
    public async Task Pass_ImageNowTrusted_Nudges()
    {
        Stuck("1", RunEndKinds.ImageUntrusted, ImageDetail());
        _sandbox = new SandboxGlobalConfig { AllowedRegistries = ["sbtscala/"] };

        (await PassAsync()).Should().Be(1);

        _nudged.Should().ContainSingle().Which.Should().Be(new ReworkNudgeRequest("p", "1", ReworkNudgeOrigin.PreconditionsMet));
    }

    [Fact]
    public async Task Pass_ImageStillUntrusted_DoesNotNudge()
    {
        Stuck("1", RunEndKinds.ImageUntrusted, ImageDetail());

        (await PassAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Pass_ReposReady_Nudges()
    {
        Repo("api", complete: true);
        Repo("web", complete: true);
        Stuck("2", RunEndKinds.BootstrapMissing, Bootstrap("api", "web"));

        await PassAsync();

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

        await PassAsync();

        Nudged().Should().BeEmpty("an incomplete, unconfigured or unnamed repository is never proof the init landed");
    }

    [Fact]
    public async Task Pass_UnreachableHost_DoesNotNudge()
    {
        Stuck("2", RunEndKinds.BootstrapMissing, Bootstrap("api"));

        await PassAsync();

        Nudged().Should().BeEmpty("an unknown answer is not Complete");
    }

    [Fact]
    public async Task Pass_Preflight_NudgedOnlyByAConfigChangeAfterTheAttempt()
    {
        Stuck("5", RunEndKinds.Preflight, new EndDetail("agents.a.model", []));

        await PassAsync();
        Nudged().Should().BeEmpty("no config was ever stored, so nothing changed");
        _configChangedAt = T0.AddMinutes(-1);
        await PassAsync();
        Nudged().Should().BeEmpty("a save before the attempt ended is what the attempt already read");
        _configChangedAt = T0.AddMinutes(1);
        await PassAsync();
        Nudged().Should().Equal("5");
    }

    [Fact]
    public async Task Pass_SliceWhoseParentRungLacksTheBootstrap_DoesNotNudge()
    {
        var branch = TicketBranchNamer.Compose(new TicketId("11")).Value;
        RepoOnBranches("api", new() { ["main"] = true, [Rung("10")] = false });
        Stuck("11", RunEndKinds.BootstrapMissing, SliceBootstrap(branch, "10"));

        await PassAsync();

        Nudged().Should().BeEmpty("the run reads the published rung, not main, so a release must too");
    }

    [Fact]
    public async Task Pass_SliceBeforeItsParentRungExists_ReadsTheDefaultBranch()
    {
        var branch = TicketBranchNamer.Compose(new TicketId("11")).Value;
        RepoOnBranches("api", new() { ["main"] = true });
        Stuck("11", RunEndKinds.BootstrapMissing, SliceBootstrap(branch, "10"));

        await PassAsync();

        Nudged().Should().Equal("11");
    }

    [Fact]
    public async Task Pass_RunBranchCarriesTheBootstrap_Nudges()
    {
        var branch = TicketBranchNamer.Compose(new TicketId("12")).Value;
        RepoOnBranches("api", new() { ["main"] = false, [branch] = true });
        Stuck("12", RunEndKinds.BootstrapMissing, new EndDetail("p", ["api"], Branch: branch));

        await PassAsync();

        Nudged().Should().Equal(["12"], "the run reads its own branch when the host has it");
    }

    [Fact]
    public async Task Pass_ReleasedThenStoppedAgainOnTheSameEvidence_NotReleasedUntilItChanges()
    {
        var branch = TicketBranchNamer.Compose(new TicketId("11")).Value;
        RepoOnBranches("api", new() { ["main"] = true, [Rung("10")] = false });
        _configChangedAt = T0.AddMinutes(1);
        Stuck("5", RunEndKinds.Preflight, new EndDetail("agents.a.model", []));
        Stuck("6", RunEndKinds.ImageUntrusted, ImageDetail());
        Stuck("11", RunEndKinds.BootstrapMissing, SliceBootstrap(branch, "10"));
        await PassAsync();
        Nudged().Should().Equal("5");

        _nudged.Clear();
        _stuck[0] = _stuck[0] with { RunId = "run-5b", FinishedAt = T0.AddMinutes(2) };
        for (var pass = 0; pass < 3; pass++) await PassAsync();
        Nudged().Should().BeEmpty("the same stop on the same evidence is not released again");

        _configChangedAt = T0.AddMinutes(3);
        _sandbox = new SandboxGlobalConfig { AllowedRegistries = ["sbtscala/"] };
        RepoOnBranches("api", new() { ["main"] = true, [Rung("10")] = true });
        await PassAsync();
        Nudged().Should().Equal("5", "6", "11");
    }

    [Fact]
    public async Task Pass_RepoNamedByTwoTickets_ProbedOnce()
    {
        var api = Repo("api", complete: true);
        Stuck("2", RunEndKinds.BootstrapMissing, Bootstrap("api"));
        Stuck("3", RunEndKinds.BootstrapMissing, Bootstrap("api"));

        await PassAsync();

        Nudged().Should().Equal("2", "3");
        api.Verify(p => p.TryReadFileAsync(".agentsmith/contexts/api/principles.md", It.IsAny<BranchName>(), It.IsAny<CancellationToken>()),
            Times.Once, "one probe per repository per pass");
    }

    [Fact]
    public async Task Pass_ProjectNoLongerConfigured_Skipped()
    {
        _configChangedAt = T0.AddMinutes(1);
        _stuck.Add(new("gone", "9", "run-9", RunEndKinds.Preflight, null, T0));

        (await PassAsync()).Should().Be(0);
    }
}
