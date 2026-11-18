using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Reviews;
using AgentSmith.Infrastructure.Services.Providers.Pipelines;
using FluentAssertions;

namespace AgentSmith.Tests.Providers.Pipelines;

/// <summary>2026-11-18-3863e: GitLab — a new pipeline on a ref with the inputs as variables, read back with its failed
/// jobs the way a merge request's head pipeline is.</summary>
public sealed class GitLabPipelineRunnerTests
{
    private const string Api = "https://gitlab.example.com/api/v4/projects/group%2Fapp";
    private readonly FakePipelineHost _host = new();

    [Fact]
    public async Task Start_PostsRefAndVariables_AndNamesThePipeline()
    {
        _host.On("POST", "/pipeline", """{"id":77,"status":"created","web_url":"https://gitlab.example.com/group/app/-/pipelines/77"}""");

        var start = await Runner().StartAsync(
            new PipelineRunOrder(null, "refs/tags/v2", new Dictionary<string, string> { ["VERSION"] = "v2" }), CancellationToken.None);

        start.Should().Be(new PipelineRunStart("77", "https://gitlab.example.com/group/app/-/pipelines/77"));
        _host.Requests.Single().Body.Should().Be("""{"ref":"v2","variables":[{"key":"VERSION","value":"v2"}]}""");
    }

    [Fact]
    public async Task Start_NamedPipeline_IsRefused() =>
        await FluentActions.Awaiting(() => Runner().StartAsync(new PipelineRunOrder("deploy", null, new Dictionary<string, string>()), default))
            .Should().ThrowAsync<ArgumentException>().WithMessage("*runs the project's own pipeline*");

    [Fact]
    public async Task Read_FailedPipeline_IsRedWithTheFailedJobs()
    {
        _host.On("GET", "/pipelines/77", """{"id":77,"status":"failed","web_url":"https://gl/p/77"}""")
            .On("GET", "/pipelines/77/jobs?scope[]=failed&per_page=100",
                """[{"id":31,"name":"rollout","stage":"deploy","web_url":"https://gl/j/31","failure_reason":"script_failure"}]""")
            .On("GET", "/jobs/31/trace", "kubectl apply\nerror: timed out");

        var run = await Runner().ReadAsync("77", new CiReadRequest(null, 4000, 5), CancellationToken.None);

        run.State.Should().Be(CiStates.Red);
        run.Url.Should().Be("https://gl/p/77");
        run.Failures.Single().Name.Should().Be("deploy: rollout");
        run.Failures.Single().Excerpt.Should().Contain("failure reason: script_failure").And.EndWith("error: timed out");
    }

    private GitLabPipelineRunner Runner() => new(_host.Http(), Api, "main");
}
