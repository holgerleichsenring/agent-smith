using AgentSmith.Application.Services;
using AgentSmith.Application.Services.Events;
using AgentSmith.Contracts.Exceptions;
using AgentSmith.Contracts.Models.ConfigStudio;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Exceptions;
using AgentSmith.Infrastructure.Core.Services;
using AgentSmith.Infrastructure.Core.Services.Configuration;
using AgentSmith.Infrastructure.Core.Services.Configuration.Studio;
using AgentSmith.Tests.TestSupport;
using FluentAssertions;
using Moq;

namespace AgentSmith.Tests.Configuration;

/// <summary>
/// 2026-11-25-9992h: a project's machine: block — loaded from YAML and stamped onto its remote repos, answered by the credential
/// resolver for every act, edited in the studio, and said in the project's setup when a hoster has none.
/// </summary>
public sealed class MachineIdentityTests
{
    private const string Head = """
        agents:
          claude: { type: Claude, model: claude-sonnet-4-20250514 }
        secrets: { token: x, bot: y }
        repos:
          app: { type: GitHub, url: https://github.com/example/app, auth: token }
          lab: { type: GitLab, url: https://gitlab.com/example/lab, auth: token }
        trackers:
          app: { type: GitHub, url: https://github.com/example/app, auth: token }
        projects:
          app:
            agent: claude
            tracker: app
            repos: [app, lab]
            pipeline: code

        """;

    [Fact]
    public void Yaml_MachineBlock_StampsEachRepoWithItsHostersIdentity()
    {
        var project = Load(Head + "    machine:\n      name: agent-smith-bot\n      email: bot@example.com\n"
            + "      github: { auth: bot, app_id: '123', installation_id: '987', email: '1+app[bot]@users.noreply.github.com' }\n");

        project.Machine!.Name.Should().Be("agent-smith-bot");
        project.ActorName.Should().Be("agent-smith-bot");
        project.Repos.Single(r => r.Name == "app").Machine.Should().Be(
            new MachineIdentity("agent-smith-bot", "1+app[bot]@users.noreply.github.com", "bot", "123", "987"));
        project.Repos.Single(r => r.Name == "lab").Machine.Should().BeNull("the block names no GitLab identity");
    }

    [Fact]
    public void Yaml_GitLabAndAzureDevOpsKeys_Bind()
    {
        var project = Load(Head + "    machine:\n      name: bot\n      email: bot@example.com\n      gitlab: { auth: bot }\n"
            + "      azure_devops: { auth: token, email: azdo-bot@example.com }\n");

        project.Repos.Single(r => r.Name == "lab").Machine.Should().Be(new MachineIdentity("bot", "bot@example.com", "bot"));
        project.Machine!.AzureDevops!.Email.Should().Be("azdo-bot@example.com");
    }

    [Fact]
    public void Yaml_NoMachineBlock_ActsAsAgentSmith_WithTheReposOwnAuth()
    {
        var project = Load(Head);

        project.Repos.Should().AllSatisfy(r => r.Machine.Should().BeNull());
        project.ActorName.Should().Be("agent-smith");
    }

    [Fact]
    public void Yaml_UnusableBlock_IsAResolutionError_NamingTheKey()
    {
        var act = () => Load(Head + "    machine:\n      email: bot@example.com\n      github: { auth: bot, app_id: '123' }\n");

        act.Should().Throw<ConfigurationException>()
            .Which.Message.Should().Contain("machine.name is empty").And.Contain("machine.github.app_id is set without installation_id");
    }

    [Fact]
    public void Findings_AHosterWithoutIdentity_IsAdvisory_NamingHosterReposAndKey()
    {
        var machine = new MachineIdentityConfig { Name = "bot", Email = "bot@example.com", Github = new MachineHosterConfig { Auth = "bot" } };

        var findings = ProjectMachineFindings.For("app", machine,
            [("app", RepoType.GitHub), ("lab", RepoType.GitLab), ("lab2", RepoType.GitLab), ("local", RepoType.Local)]).ToList();

        var finding = findings.Should().ContainSingle().Which;
        finding.Severity.Should().Be(StartupFindingSeverity.Advisory);
        finding.Reason.Should().Contain("no machine identity for GitLab").And.Contain("lab, lab2").And.Contain("machine.gitlab");
        ProjectMachineFindings.Missing("app", null, [("app", RepoType.GitHub)]).Should().ContainSingle();
        ProjectMachineFindings.Missing([new ResolvedProject { Name = "p", Repos = [new RepoConnection { Name = "r", Type = RepoType.GitLab }] }])
            .Should().ContainSingle().Which.Project.Should().Be("p");
    }

    [Fact]
    public void Findings_UnusableBlock_IsBlocking_PerKey()
    {
        var machine = new MachineIdentityConfig
        {
            Name = "bot", Gitlab = new MachineHosterConfig { Auth = "", AppId = "1", InstallationId = "2" },
        };

        ProjectMachineFindings.Unusable("app", machine).Select(f => (f.Field, f.IsBlocking)).Should().BeEquivalentTo(
            [("machine.gitlab.auth", true), ("machine.email", true), ("machine.gitlab.app_id", true)]);
        ProjectMachineFindings.Unusable("app", new MachineIdentityConfig()).Should().BeEmpty("a block with no hoster acts on nothing");
    }

    [Fact]
    public void CredentialResolver_MachineStamp_AnswersTheMachinesToken_AndAnAppMints()
    {
        var tokens = new Mock<IMachineTokens>();
        var resolver = new CredentialResolver(new LoadedSecretValues(
            TestCredentials.Config(("token", "persons-pat"), ("bot", "bots-pat"), ("key", "pem"))), machineTokens: tokens.Object);
        var repo = new RepoConnection { Name = "app", Type = RepoType.GitHub, Auth = "token" };
        var app = new MachineIdentity("bot", "bot@example.com", "key", "123", "987");
        tokens.Setup(t => t.InstallationToken(It.IsAny<MachineTarget>(), app, "pem")).Returns("ghs_minted");
        tokens.Setup(t => t.AppLogin(It.IsAny<MachineTarget>(), app, "pem")).Returns("bot[bot]");

        resolver.For(repo).Should().Be("persons-pat");
        resolver.For(repo with { Machine = new MachineIdentity("bot", "bot@example.com", "bot") }).Should().Be("bots-pat");
        resolver.For(repo with { Machine = app }).Should().Be("ghs_minted");
        resolver.MachineLogin(repo with { Machine = app }).Should().Be("bot[bot]");
        resolver.MachineLogin(repo).Should().BeNull("a user token says itself who it is");
    }

    [Fact]
    public void CredentialResolver_AppWithoutAMinter_RefusesAsAMissingCredential()
    {
        var resolver = new CredentialResolver(new LoadedSecretValues(TestCredentials.Config(("key", "pem"))));
        var repo = new RepoConnection { Name = "app", Machine = new MachineIdentity("bot", "e", "key", "1", "2") };

        var act = () => resolver.For(repo);

        act.Should().Throw<MissingCredentialException>().Which.Should().BeOfType<MachineCredentialException>();
    }

    [Fact]
    public void Studio_MachineBlock_RoundTrips_AndAnEmptyBlockClearsIt()
    {
        var sent = new ProjectEntity("app", "a", "t", [], null, [], Machine: new ProjectMachine("bot", "bot@example.com",
            Github: new ProjectMachineHoster("key", "123", "987", "1+app[bot]@users.noreply.github.com"),
            AzureDevops: new ProjectMachineHoster("azdo")));

        var stored = RawProjectPatch.Apply(sent, new RawProjectEntry());

        stored.Machine!.Github!.InstallationId.Should().Be("987");
        ProjectEntityMapping.ToProject("app", stored).Machine.Should().Be(sent.Machine);
        RawProjectPatch.Apply(sent with { Machine = null }, stored).Machine.Should().NotBeNull("an absent block leaves the stored one alone");
        RawProjectPatch.Apply(sent with { Machine = new ProjectMachine("", "") }, stored).Machine.Should().BeNull();
    }

    [Fact]
    public void DraftRules_SayAHosterWithoutIdentity_OnTheProjectForm()
    {
        var catalog = new ConfigCatalog() with
        {
            Repos = [new RepoEntity("app", "https://github.com/example/app", null)],
            Connections = [new ConnectionEntity("corp", "azure_devops", "org", "proj", "pat", null)],
        };
        var draft = new ProjectEntity("app", "a", "t", ["app", "corp/server"], null, []);

        var findings = new ConfigDraftRules(new EffectiveTriggerBuilder(), new ProjectConfigNormalizer()).ForProject(draft, catalog);

        findings.Where(f => f.Field == "machine").Select(f => f.Reason).Should().SatisfyRespectively(
            github => github.Should().Contain("GitHub").And.Contain("app"),
            azure => azure.Should().Contain("Azure DevOps").And.Contain("corp/server"));
    }

    [Fact]
    public void SeriesMessages_CarryTheMachinesName_InTheMastersRole()
    {
        AgentSmith.Server.Services.Series.SeriesTicketMessage.AuthorOf(new ResolvedProject
        {
            Machine = new MachineIdentityConfig { Name = "agent-smith-bot", Gitlab = new MachineHosterConfig { Auth = "bot" } },
        }).Should().Be("agent-smith-bot (series master)");
        new ResolvedProject { Machine = new MachineIdentityConfig { Name = "named-only" } }.ActorName
            .Should().Be("agent-smith", "a name with no hoster acts under no token of its own");
        AgentSmith.Server.Services.Series.SeriesTicketMessage.AuthorOf(null).Should().Be("agent-smith (series master)");
    }

    [Fact]
    public void Approval_NoTypeApprovesAPullRequest()
    {
        var names = new[]
            {
                typeof(CredentialResolver).Assembly, // Contracts
                typeof(AgentSmith.Application.Services.Handlers.PullRequestMergeSequence).Assembly,
                typeof(AgentSmith.Infrastructure.Services.Providers.Source.GitHubAppTokens).Assembly,
            }
            .SelectMany(a => a.GetTypes()).Select(t => t.Name).ToList();

        names.Should().NotContain(["IPullRequestApprover", "AzureReposApprovalVote", "GitHubApprover", "GitLabApprover"],
            "the machine never approves its own pull request; a review a host requires is a person's");
    }

    [Fact]
    public void Yaml_AzureServicePrincipal_StampsTenantAndClient()
    {
        var project = Load(Head.Replace("lab: { type: GitLab, url: https://gitlab.com/example/lab, auth: token }",
                "lab: { type: AzureDevOps, url: https://dev.azure.com/example/p/_git/lab, auth: token }")
            + "    machine:\n      name: bot\n      email: bot@example.com\n"
            + "      azure_devops: { auth: bot, tenant_id: tenant-1, client_id: client-1 }\n");

        var machine = project.Repos.Single(r => r.Name == "lab").Machine!;
        machine.Should().Be(new MachineIdentity("bot", "bot@example.com", "bot", TenantId: "tenant-1", ClientId: "client-1"));
        machine.IsAzurePrincipal.Should().BeTrue();
    }

    [Fact]
    public void Findings_AServicePrincipalHalfSet_OrOnAnotherHoster_OrBesideAnApp_IsBlocking()
    {
        MachineIdentityConfig Block(MachineHosterConfig? github = null, MachineHosterConfig? azure = null) =>
            new() { Name = "bot", Email = "bot@example.com", Github = github, AzureDevops = azure };

        ProjectMachineFindings.Unusable("app", Block(azure: new() { Auth = "s", TenantId = "t" })).Select(f => f.Field)
            .Should().Equal("machine.azure_devops.client_id");
        ProjectMachineFindings.Unusable("app", Block(github: new() { Auth = "s", TenantId = "t", ClientId = "c" })).Select(f => f.Field)
            .Should().Equal("machine.github.tenant_id");
        ProjectMachineFindings.Unusable("app", Block(azure: new() { Auth = "s", TenantId = "t", ClientId = "c", AppId = "1", InstallationId = "2" }))
            .Select(f => f.Field).Should().Contain("machine.azure_devops.app_id");
        ProjectMachineFindings.Unusable("app", Block(azure: new() { Auth = "s", TenantId = "t", ClientId = "c" })).Should().BeEmpty();
    }

    [Fact]
    public void CredentialResolver_ServicePrincipal_MintsItsEntraToken_FromTheClientSecret()
    {
        var tokens = new Mock<IMachineTokens>();
        var resolver = new CredentialResolver(new LoadedSecretValues(TestCredentials.Config(("sp", "client-secret"))), machineTokens: tokens.Object);
        var principal = new MachineIdentity("bot", "bot@example.com", "sp", TenantId: "t", ClientId: "c");
        var repo = new RepoConnection { Name = "lab", Type = RepoType.AzureDevOps, Url = "https://dev.azure.com/o/p/_git/lab", Machine = principal };
        tokens.Setup(t => t.PrincipalToken(It.Is<MachineTarget>(m => m.Url == repo.Url && m.Entity.Contains("Repo 'lab'")), principal, "client-secret"))
            .Returns("eyJ.entra.token");

        resolver.For(repo).Should().Be("eyJ.entra.token");
        resolver.MachineLogin(repo).Should().BeNull("an Entra token says itself who it is (connectionData)");
    }

    [Fact]
    public void Studio_ServicePrincipal_RoundTrips()
    {
        var sent = new ProjectEntity("app", "a", "t", [], null, [], Machine: new ProjectMachine("bot", "bot@example.com",
            AzureDevops: new ProjectMachineHoster("sp", TenantId: "tenant-1", ClientId: "client-1")));

        var stored = RawProjectPatch.Apply(sent, new RawProjectEntry());

        (stored.Machine!.AzureDevops!.TenantId, stored.Machine.AzureDevops.ClientId).Should().Be(("tenant-1", "client-1"));
        ProjectEntityMapping.ToProject("app", stored).Machine.Should().Be(sent.Machine);
    }

    private static ResolvedProject Load(string yaml)
    {
        var path = Path.Combine(Path.GetTempPath(), $"machine-identity-{Guid.NewGuid():N}.yml");
        File.WriteAllText(path, yaml);
        try
        {
            var loader = new YamlConfigurationLoader(new RawConfigMaterializer(new ProjectConfigNormalizer(),
                new EffectiveTriggerBuilder(), new DeploymentDefaultsApplier(), new ConfigCatalogResolver(),
                new AgentSmithPaths()), new NoOpSystemEventPublisher());
            return loader.LoadConfig(path).Projects["app"];
        }
        finally
        {
            File.Delete(path);
        }
    }
}
