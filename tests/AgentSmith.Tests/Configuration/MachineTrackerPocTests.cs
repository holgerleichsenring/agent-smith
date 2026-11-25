using AgentSmith.Application.Services.Pocs;
using AgentSmith.Application.Services.Pocs.Pipeline;
using AgentSmith.Application.Services.Preflight.Checks;
using AgentSmith.Application.Services.Rework;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Services;
using AgentSmith.Infrastructure.Core.Services.Configuration;
using AgentSmith.Infrastructure.Services.Factories;
using AgentSmith.Infrastructure.Services.Providers.Tickets;
using AgentSmith.Tests.TestSupport;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Octokit;

namespace AgentSmith.Tests.Configuration;

/// <summary>
/// 2026-11-25-0873: tracker writes, PoC acts and the preflight repo probes run under the machine identity — a tracker stamped
/// from its project's block answers the machine's token and an App's {slug}[bot] as self; a PoC repository takes pocs.machine;
/// preflight probes a project's repos as they act; a tracker with no or with several identities is said.
/// </summary>
public sealed class MachineTrackerPocTests
{
    private static readonly MachineIdentity Bot = new("bot", "bot@example.com", "bot");
    private static readonly MachineIdentity App = new("bot", "1+app[bot]@users.noreply.github.com", "key", "123", "987");
    private static readonly TrackerConnection Tracker = new() { Name = "issues", Type = TrackerType.GitHub, Url = "https://github.com/example/app", Auth = "token" };

    private static ICredentialResolver Resolver(Mock<IMachineTokens>? tokens = null) => new CredentialResolver(
        new LoadedSecretValues(TestCredentials.Config(("token", "persons-pat"), ("bot", "bots-pat"), ("key", "pem"))), machineTokens: tokens?.Object);

    [Fact]
    public void CredentialResolver_StampedTracker_WritesWithTheMachinesToken_AnAppMintedAgainstTheTrackersHost()
    {
        var tokens = new Mock<IMachineTokens>();
        tokens.Setup(t => t.InstallationToken(It.Is<MachineTarget>(m => m.Url == Tracker.Url && m.Entity.Contains("Tracker 'issues'")), App, "pem"))
            .Returns("ghs_minted");
        tokens.Setup(t => t.AppLogin(It.IsAny<MachineTarget>(), App, "pem")).Returns("app[bot]");
        var resolver = Resolver(tokens);

        resolver.For(Tracker).Should().Be("persons-pat");
        resolver.For(Tracker with { Machine = Bot }).Should().Be("bots-pat");
        resolver.For(Tracker with { Machine = App }).Should().Be("ghs_minted");
        resolver.MachineLogin(Tracker with { Machine = App }).Should().Be("app[bot]");
        resolver.MachineLogin(Tracker with { Machine = Bot }).Should().BeNull();
    }

    [Fact]
    public void TrackerConnections_GitHubApp_CarriesTheMachinesTokenAndItsLogin()
    {
        var tokens = new Mock<IMachineTokens>();
        tokens.Setup(t => t.InstallationToken(It.IsAny<MachineTarget>(), App, "pem")).Returns("ghs_minted");
        tokens.Setup(t => t.AppLogin(It.IsAny<MachineTarget>(), App, "pem")).Returns("app[bot]");

        var connection = new TrackerConnections(Resolver(tokens)).GitHub(Tracker with { Machine = App });

        connection.Token.Should().Be("ghs_minted");
        connection.SelfLogin!.Invoke().Should().Be("app[bot]");
        new TrackerConnections(Resolver()).GitHub(Tracker).SelfLogin.Should().BeNull("a user token answers GET /user itself");
    }

    [Fact]
    public async Task GitHubStatusHistory_AppSelf_IsItsBotLogin_WithoutGetUser()
    {
        var client = new Mock<IGitHubClient>(MockBehavior.Strict);

        var self = await new GitHubStatusHistory(client.Object, "example", "app", () => "app[bot]").SelfAsync(CancellationToken.None);

        self.Should().Be(new TrackerActor("app[bot]", "app[bot]", IsApp: true));
    }

    [Fact]
    public async Task TrackerIdentity_KeepsOneSelf_PerTrackerAndMachine()
    {
        var factory = new Mock<ITicketProviderFactory>();
        var asked = 0;
        factory.Setup(f => f.Create(It.IsAny<TrackerConnection>())).Returns((TrackerConnection t) =>
        {
            asked++;
            var self = new Mock<ITicketProvider>().As<ITrackerSelf>();
            self.Setup(s => s.SelfAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new TrackerActor(t.Machine?.Name ?? "person"));
            return (ITicketProvider)self.Object;
        });
        var identity = new TrackerIdentity(factory.Object, NullLogger<TrackerIdentity>.Instance);

        (await identity.SelfAsync(Tracker with { Machine = Bot }, CancellationToken.None))!.Id.Should().Be("bot");
        (await identity.SelfAsync(Tracker, CancellationToken.None))!.Id.Should().Be("person");
        (await identity.SelfAsync(Tracker with { Machine = Bot }, CancellationToken.None))!.Id.Should().Be("bot");
        asked.Should().Be(2);
    }

    [Fact]
    public void TrackerFindings_SayAHosterWithoutIdentity_AndATrackerWrittenUnderSeveralIdentities()
    {
        var machine = new MachineIdentityConfig { Name = "bot", Email = "bot@example.com", Github = new() { Auth = "bot" } };
        ResolvedProject Project(string name, MachineIdentityConfig? block) =>
            new() { Name = name, Machine = block, Tracker = Tracker with { Machine = block?.IdentityFor(TrackerType.GitHub) } };

        TrackerMachineFindings.Missing("a", null, "issues", TrackerType.GitHub).Single().Reason.Should()
            .Contain("tracker 'issues'").And.Contain("machine.github");
        TrackerMachineFindings.Missing("a", null, "jira", TrackerType.Jira).Should().BeEmpty("Jira has no machine identity");
        TrackerMachineFindings.For([Project("a", machine), Project("b", machine)]).Should().BeEmpty();

        var mixed = TrackerMachineFindings.For([Project("a", machine), Project("b", null)]).ToList();
        mixed.Where(f => f.Reason.Contains("different identities")).Select(f => f.Project).Should().BeEquivalentTo(["a", "b"]);
        mixed.Should().OnlyContain(f => !f.IsBlocking);
    }

    [Fact]
    public void PocMachine_StampsAnUnstampedRepo_KeepsAProjectsOwnMachine()
    {
        var pocs = new PocsConfig { Machine = new MachineIdentityConfig { Name = "poc-bot", Email = "poc@example.com", Github = new() { Auth = "poc_key" } } };
        var repo = new RepoConnection { Name = "poc-source", Type = RepoType.GitHub, Url = "https://github.com/example/pocs", Auth = "token" };

        PocMachine.Stamp(pocs, repo).Machine.Should().Be(new MachineIdentity("poc-bot", "poc@example.com", "poc_key"));
        PocMachine.Stamp(pocs, repo with { Machine = Bot }).Machine.Should().Be(Bot, "the poc- project's own block wins");
        PocMachine.Stamp(new PocsConfig(), repo).Machine.Should().BeNull();

        var config = new AgentSmithConfig { Repos = new Dictionary<string, RepoConnection> { ["poc-source"] = repo }, Pocs = pocs };
        new PocSourceRepos(new Mock<IConnectionRepoUrlBuilder>().Object).Resolve(config, new PocTargetConfig { Name = "main", SourceRepo = "poc-source" })
            .Repo!.Machine!.Name.Should().Be("poc-bot", "the target install acts under pocs.machine");
    }

    [Fact]
    public void PocFindings_ATargetsHosterWithoutPocsMachine_IsAdvisory_AndAnUnusableBlockIsAProblem()
    {
        var repo = new RepoConnection { Name = "poc-source", Type = RepoType.GitHub, Url = "https://github.com/example/pocs" };
        var config = new AgentSmithConfig
        {
            Repos = new Dictionary<string, RepoConnection> { ["poc-source"] = repo },
            Pocs = new PocsConfig { Enabled = true, Targets = [new PocTargetConfig { Name = "main", SourceRepo = "poc-source" }] },
        };

        PocMachineFindings.Missing(config).Single().Reason.Should().Contain("PoC target 'main'").And.Contain("pocs.machine.github");
        config.Pocs.Machine = new MachineIdentityConfig { Name = "poc-bot", Email = "poc@example.com", Github = new() { Auth = "poc_key" } };
        PocMachineFindings.Missing(config).Should().BeEmpty();

        config.Pocs.Machine = new MachineIdentityConfig { Email = "poc@example.com", Github = new() { Auth = "" } };
        config.Pocs.Problems().Should().Contain(p => p.Contains("pocs.machine.name")).And.Contain(p => p.Contains("pocs.machine.github.auth"));
    }

    [Fact]
    public void PreflightRepos_ProbeAProjectsRepoAsItActs_AndEachOtherIdentityOnce()
    {
        var app = new RepoConnection { Name = "app", Type = RepoType.GitHub, Url = "https://github.com/example/app", Auth = "token" };
        var lone = new RepoConnection { Name = "lone", Type = RepoType.GitHub, Url = "https://github.com/example/lone", Auth = "token" };
        var config = new AgentSmithConfig
        {
            Repos = new Dictionary<string, RepoConnection> { ["app"] = app, ["lone"] = lone },
            Projects = new Dictionary<string, ResolvedProject>
            {
                ["a"] = new() { Name = "a", Repos = [app with { Machine = Bot }] },
                ["b"] = new() { Name = "b", Repos = [app] },
            },
        };

        PreflightRepos.Acting(config).Should().BeEquivalentTo([app with { Machine = Bot }, app, lone],
            "the machine's and the repo's own reach of one url are both probed, each once");
    }
}
