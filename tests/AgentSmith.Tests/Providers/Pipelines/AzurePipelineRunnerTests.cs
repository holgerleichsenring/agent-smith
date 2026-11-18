using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Reviews;
using AgentSmith.Infrastructure.Services.Providers.Pipelines;
using FluentAssertions;
using Microsoft.TeamFoundation.Build.WebApi;
using Microsoft.TeamFoundation.Core.WebApi;
using Microsoft.VisualStudio.Services.Common;
using Moq;

namespace AgentSmith.Tests.Providers.Pipelines;

/// <summary>2026-11-18-3863e: Azure DevOps — a run of the named pipeline with template parameters on a full ref, read
/// back as the build it is; a failed one tells its failed tasks, as a pull request's failed build does.</summary>
public sealed class AzurePipelineRunnerTests
{
    private static readonly Guid ProjectId = Guid.NewGuid();
    private readonly Mock<BuildHttpClient> _build = new(new Uri("https://localhost/fake"), new VssCredentials(new VssBasicCredential(string.Empty, "fake")));
    private Build? _queued;

    public AzurePipelineRunnerTests()
    {
        Definitions(new BuildDefinitionReference { Id = 12, Name = "deploy" });
        _build.Setup(c => c.QueueBuildAsync(It.IsAny<Build>(), "demo", It.IsAny<bool?>(), It.IsAny<string>(), It.IsAny<int?>(),
                It.IsAny<int?>(), It.IsAny<object>(), It.IsAny<CancellationToken>()))
            .Callback<Build, string, bool?, string, int?, int?, object, CancellationToken>((b, _, _, _, _, _, _, _) => _queued = b)
            .ReturnsAsync(new Build { Id = 501 });
    }

    [Fact]
    public async Task Start_FindsThePipelineByNameAndQueuesItWithTemplateParametersOnAFullRef()
    {
        var start = await Runner().StartAsync(
            new PipelineRunOrder("deploy", "release", new Dictionary<string, string> { ["version"] = "v2" }), CancellationToken.None);

        start.Should().Be(new PipelineRunStart("501", "https://dev.azure.com/example/demo/_build/results?buildId=501"));
        _queued!.Definition.Id.Should().Be(12);
        _queued.SourceBranch.Should().Be("refs/heads/release");
        _queued.TemplateParameters["version"].Should().Be("v2");
    }

    [Fact]
    public async Task Start_UnknownPipeline_IsRefusedNamingIt()
    {
        Definitions();

        await FluentActions.Awaiting(() => Runner().StartAsync(new PipelineRunOrder("ops\\deploy", null, new Dictionary<string, string>()), default))
            .Should().ThrowAsync<InvalidOperationException>().WithMessage("*no pipeline named 'ops\\deploy'*");
    }

    [Fact]
    public async Task Read_FailedRun_IsRedWithTheFailedTasksIssues()
    {
        _build.Setup(c => c.GetBuildAsync("demo", 501, It.IsAny<string>(), It.IsAny<object>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Build
            {
                Id = 501, Status = BuildStatus.Completed, Result = BuildResult.Failed,
                Project = new TeamProjectReference { Id = ProjectId }, Definition = new DefinitionReference { Name = "deploy" },
            });
        var timeline = (Timeline)Activator.CreateInstance(typeof(Timeline), nonPublic: true)!;
        timeline.Records.Add(new TimelineRecord
        {
            Name = "helm upgrade", RecordType = "Task", Result = TaskResult.Failed,
            Issues = { new Issue { Type = IssueType.Error, Message = "release failed" } },
        });
        _build.Setup(c => c.GetBuildTimelineAsync(ProjectId, 501, It.IsAny<Guid?>(), It.IsAny<int?>(), It.IsAny<Guid?>(), It.IsAny<object>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(timeline);

        var run = await Runner().ReadAsync("501", new CiReadRequest(null, 4000, 5), CancellationToken.None);

        run.State.Should().Be(CiStates.Red);
        run.Failures.Single().Name.Should().Be("deploy");
        run.Failures.Single().Excerpt.Should().Contain("task failed: helm upgrade").And.Contain("release failed");
    }

    private AzurePipelineRunner Runner() => new(_ => Task.FromResult(_build.Object), "demo", "https://dev.azure.com/example");

    private void Definitions(params BuildDefinitionReference[] found) =>
        _build.Setup(c => c.GetDefinitionsAsync("demo", It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<DefinitionQueryOrder?>(), It.IsAny<int?>(), It.IsAny<string>(), It.IsAny<DateTime?>(), It.IsAny<IEnumerable<int>>(),
                It.IsAny<string>(), It.IsAny<DateTime?>(), It.IsAny<DateTime?>(), It.IsAny<bool?>(), It.IsAny<Guid?>(),
                It.IsAny<int?>(), It.IsAny<string>(), It.IsAny<object>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<BuildDefinitionReference>(found));
}
