using AgentSmith.Application.Services.Triggers;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Models.Triggers;
using FluentAssertions;

namespace AgentSmith.Tests.Triggers;

/// <summary>
/// p0140a: ProjectResolver maps an IncomingTicketEnvelope to the list of (project, pipeline)
/// matches across all configured projects' trigger blocks. Multi-match is intentional;
/// zero-match returns an empty list (the caller decides what to do).
/// </summary>
public sealed class ProjectResolverTests
{
    private readonly ProjectResolver _sut = new();

    [Fact]
    public void TagStrategy_MatchesTicketWithTag()
    {
        var config = ConfigWith(("alpha", new TrackerConnection { Name = "gh", Type = TrackerType.GitHub },
            project => project with { GithubTrigger = TriggerWithTag("alpha") }));

        var matches = _sut.Resolve(config, new IncomingTicketEnvelope { Labels = ["alpha"] });

        matches.Should().ContainSingle(m => m.ProjectName == "alpha");
    }

    [Fact]
    public void TagStrategy_TicketHasNoMatchingTag_ReturnsEmpty()
    {
        var config = ConfigWith(("alpha", new TrackerConnection { Name = "gh", Type = TrackerType.GitHub },
            project => project with { GithubTrigger = TriggerWithTag("alpha") }));

        var matches = _sut.Resolve(config, new IncomingTicketEnvelope { Labels = ["other"] });

        matches.Should().BeEmpty();
    }

    [Fact]
    public void TagStrategy_TicketMatchesTwoProjects_ReturnsBoth()
    {
        var config = ConfigWith(
            ("alpha", new TrackerConnection { Name = "gh1", Type = TrackerType.GitHub },
                project => project with { GithubTrigger = TriggerWithTag("shared") }),
            ("beta", new TrackerConnection { Name = "gh2", Type = TrackerType.GitHub },
                project => project with { GithubTrigger = TriggerWithTag("shared") }));

        var matches = _sut.Resolve(config, new IncomingTicketEnvelope { Labels = ["shared"] });

        matches.Should().HaveCount(2);
        matches.Select(m => m.ProjectName).Should().BeEquivalentTo(new[] { "alpha", "beta" });
    }

    [Fact]
    public void AreaPathStrategy_MatchesExactPath()
    {
        var config = ConfigWith(("p", new TrackerConnection { Name = "ado", Type = TrackerType.AzureDevOps },
            project => project with
            {
                AzuredevopsTrigger = TriggerWithResolution(
                    new ProjectResolutionConfig { Strategy = ResolutionStrategy.AreaPath, Value = "Contoso\\Billing" })
            }));

        var matches = _sut.Resolve(config, new IncomingTicketEnvelope { AreaPath = "Contoso\\Billing" });

        matches.Should().ContainSingle(m => m.ProjectName == "p");
    }

    [Fact]
    public void AreaPathStrategy_MatchesSubtreeBelowConfiguredRoot()
    {
        var config = ConfigWith(("p", new TrackerConnection { Name = "ado", Type = TrackerType.AzureDevOps },
            project => project with
            {
                AzuredevopsTrigger = TriggerWithResolution(
                    new ProjectResolutionConfig { Strategy = ResolutionStrategy.AreaPath, Value = "Contoso/Billing" })
            }));

        var matches = _sut.Resolve(config, new IncomingTicketEnvelope { AreaPath = "Contoso\\Billing\\Invoicing\\Drafts" });

        matches.Should().ContainSingle(m => m.ProjectName == "p");
    }

    [Fact]
    public void AreaPathStrategy_DoesNotMatchSiblingPath()
    {
        var config = ConfigWith(("p", new TrackerConnection { Name = "ado", Type = TrackerType.AzureDevOps },
            project => project with
            {
                AzuredevopsTrigger = TriggerWithResolution(
                    new ProjectResolutionConfig { Strategy = ResolutionStrategy.AreaPath, Value = "Contoso\\Billing" })
            }));

        var matches = _sut.Resolve(config, new IncomingTicketEnvelope { AreaPath = "Contoso\\BillingOther" });

        matches.Should().BeEmpty();
    }

    [Fact]
    public void PipelineFromLabel_ConfiguredButNoLabelMatches_DropsMatch()
    {
        var trigger = TriggerWithTag("alpha");
        trigger.PipelineFromLabel = new Dictionary<string, string>
        {
            ["agent-smith:bug"] = "fix-bug",
            ["agent-smith:feature"] = "add-feature",
        };
        var config = ConfigWith(("alpha", new TrackerConnection { Name = "gh", Type = TrackerType.GitHub },
            project => project with { GithubTrigger = trigger }));

        var matches = _sut.Resolve(config, new IncomingTicketEnvelope { Labels = ["alpha"] });

        matches.Should().BeEmpty();
    }

    [Fact]
    public void PipelineFromLabel_ConfiguredAndLabelMatches_ReturnsMappedPipeline()
    {
        var trigger = TriggerWithTag("alpha");
        trigger.PipelineFromLabel = new Dictionary<string, string>
        {
            ["agent-smith:bug"] = "fix-bug",
            ["agent-smith:feature"] = "add-feature",
        };
        var config = ConfigWith(("alpha", new TrackerConnection { Name = "gh", Type = TrackerType.GitHub },
            project => project with { GithubTrigger = trigger }));

        var matches = _sut.Resolve(config, new IncomingTicketEnvelope { Labels = ["alpha", "agent-smith:feature"] });

        matches.Should().ContainSingle()
            .Which.PipelineName.Should().Be("add-feature");
    }

    [Fact]
    public void NoPipelineFromLabel_UsesDefaultPipeline()
    {
        var config = ConfigWith(("alpha", new TrackerConnection { Name = "gh", Type = TrackerType.GitHub },
            project => project with { GithubTrigger = TriggerWithTag("alpha") }));

        var matches = _sut.Resolve(config, new IncomingTicketEnvelope { Labels = ["alpha"] });

        matches.Should().ContainSingle()
            .Which.PipelineName.Should().Be("fix-bug");
    }

    [Fact]
    public void GlobalPipelineTriggers_ConfiguredButNoLabelMatches_DropsMatch()
    {
        var globalTriggers = new PipelineTriggerMap(new Dictionary<string, string>
        {
            ["agent-smith:bug"] = "fix-bug",
            ["agent-smith:feature"] = "add-feature",
        });
        var config = ConfigWith(globalTriggers,
            ("alpha", new TrackerConnection { Name = "gh", Type = TrackerType.GitHub },
                project => project with { GithubTrigger = TriggerWithTag("alpha") }));

        var matches = _sut.Resolve(config, new IncomingTicketEnvelope { Labels = ["alpha"] });

        matches.Should().BeEmpty();
    }

    [Fact]
    public void RepoStrategy_MatchesByRepoUrl()
    {
        var url = "https://github.com/acme/app.git";
        var config = ConfigWith(("acme", new TrackerConnection { Name = "gh", Type = TrackerType.GitHub },
            project => project with
            {
                Repos = new[] { new RepoConnection { Name = "acme", Url = url } },
                GithubTrigger = TriggerWithResolution(
                    new ProjectResolutionConfig { Strategy = ResolutionStrategy.Repo, Value = url })
            }));

        var matches = _sut.Resolve(config, new IncomingTicketEnvelope { SourceRepoUrl = url });

        matches.Should().ContainSingle(m => m.ProjectName == "acme");
    }

    private static AgentSmithConfig ConfigWith(
        params (string Name, TrackerConnection Tracker, Func<ResolvedProject, ResolvedProject> Shape)[] entries)
        => ConfigWith(PipelineTriggerMap.Empty, entries);

    private static AgentSmithConfig ConfigWith(
        PipelineTriggerMap globalTriggers,
        params (string Name, TrackerConnection Tracker, Func<ResolvedProject, ResolvedProject> Shape)[] entries)
    {
        var projects = new Dictionary<string, ResolvedProject>();
        foreach (var (name, tracker, shape) in entries)
        {
            var project = new ResolvedProject
            {
                Name = name,
                Tracker = tracker,
                DefaultPipeline = "fix-bug",
            };
            projects[name] = shape(project);
        }
        return new AgentSmithConfig { Projects = projects, PipelineTriggers = globalTriggers };
    }

    private static WebhookTriggerConfig TriggerWithTag(string tag) =>
        TriggerWithResolution(new ProjectResolutionConfig { Strategy = ResolutionStrategy.Tag, Value = tag });

    private static WebhookTriggerConfig TriggerWithResolution(ProjectResolutionConfig resolution) =>
        new() { ProjectResolution = resolution, DefaultPipeline = "fix-bug" };
}
