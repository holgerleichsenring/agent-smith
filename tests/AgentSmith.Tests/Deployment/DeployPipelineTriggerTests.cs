using AgentSmith.Contracts.Events;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Reviews;
using FluentAssertions;
using Moq;

namespace AgentSmith.Tests.Deployment;

/// <summary>2026-11-18-3863e: a template that delivers by the host's pipeline — the deploy step starts it with the filled
/// inputs and names the started run and its state; a promoted environment takes the source manifest's version.</summary>
public sealed class DeployPipelineTriggerTests
{
    public const string Declaration = """
        delivery: pipeline_trigger
        parameters:
          - name: image
            default: registry.example/app
          - name: replicas
        environments:
          - name: test
            pipeline: deploy.yml
            inputs: { version: "{version}", target: "{environment}", image: "{image}" }
            manifest: deploy/test/kustomization.yaml
            version_key: images.0.newTag
          - name: prod
            pipeline: deploy.yml
            ref: release
            inputs: { tag: "{context}-{version}" }
            promotes_from: test
        """;

    private readonly DeployFixture _fixture = new();

    public DeployPipelineTriggerTests()
    {
        _fixture.Template.Files[DeployFixture.DeclarationPath] = Declaration;
        _fixture.Runner.Setup(r => r.StartAsync(It.IsAny<PipelineRunOrder>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PipelineRunStart("42", "https://ci.example/runs/42"));
        Reads(new PipelineRunState(CiStates.Pending, "https://ci.example/runs/42", []));
    }

    [Fact]
    public async Task Execute_EntryEnvironment_StartsThePipelineWithTheFilledInputsAndNamesTheRun()
    {
        var result = await _fixture.Handler().ExecuteAsync(DeployFixture.Context("test", "v2"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue(result.Message);
        result.Message.Should().Contain("Deploy of `api` v2 to test").And.Contain("started run https://ci.example/runs/42; it is running");
        _fixture.Runner.Verify(r => r.StartAsync(It.Is<PipelineRunOrder>(o => o.Pipeline == "deploy.yml" && o.Ref == null
            && o.Inputs["version"] == "v2" && o.Inputs["target"] == "test" && o.Inputs["image"] == "registry.example/app"),
            It.IsAny<CancellationToken>()), Times.Once);
        _fixture.Published.OfType<DeployDeliveredEvent>().Single().PipelineRunId.Should().Be("42"); // 2026-11-18-3863f
    }

    [Fact]
    public async Task Execute_PromotedEnvironment_TakesTheSourceManifestsVersion()
    {
        var result = await _fixture.Handler().ExecuteAsync(DeployFixture.Context("prod", null), CancellationToken.None);

        result.IsSuccess.Should().BeTrue(result.Message);
        _fixture.Runner.Verify(r => r.StartAsync(It.Is<PipelineRunOrder>(o => o.Ref == "release" && o.Inputs["tag"] == "api-v1"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Execute_RedRun_NamesTheFailedJobAndItsLink()
    {
        Reads(new PipelineRunState(CiStates.Red, "https://ci.example/runs/42", [new CiFailure("deploy", "https://ci.example/jobs/7", "boom")]));

        var result = await _fixture.Handler().ExecuteAsync(DeployFixture.Context("test", "v2"), CancellationToken.None);

        result.Message.Should().Contain("it failed: deploy (https://ci.example/jobs/7)");
    }

    [Fact]
    public async Task Execute_HostRefusesOrRunsNoPipeline_FailsInItsWords()
    {
        _fixture.Runner.Setup(r => r.StartAsync(It.IsAny<PipelineRunOrder>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("HTTP 422: Workflow does not have 'workflow_dispatch' trigger"));
        var refused = await _fixture.Handler().ExecuteAsync(DeployFixture.Context("test", "v2"), CancellationToken.None);
        _fixture.HostRunsPipelines = false;
        var none = await _fixture.Handler().ExecuteAsync(DeployFixture.Context("test", "v2"), CancellationToken.None);

        refused.IsSuccess.Should().BeFalse();
        refused.Message.Should().Contain("could not be started: HTTP 422");
        none.Message.Should().Contain("whose host runs no pipeline");
    }

    [Fact]
    public async Task Execute_ParameterWithNoValue_IsRefused()
    {
        _fixture.Template.Files[DeployFixture.DeclarationPath] = Declaration.Replace("image: \"{image}\"", "replicas: \"{replicas}\"");

        var result = await _fixture.Handler().ExecuteAsync(DeployFixture.Context("test", "v2"), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Message.Should().Contain("parameter 'replicas' has no value in the binding and no default in the template");
    }

    [Fact]
    public async Task Execute_HostDidNotNameTheRun_StartsAndSaysSo()
    {
        _fixture.Runner.Setup(r => r.StartAsync(It.IsAny<PipelineRunOrder>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PipelineRunStart(null, "https://ghe.example/o/r/actions/workflows/deploy.yml"));

        var result = await _fixture.Handler().ExecuteAsync(DeployFixture.Context("test", "v2"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Message.Should().Contain("did not name the run").And.Contain("actions/workflows/deploy.yml");
    }

    private void Reads(PipelineRunState state) =>
        _fixture.Runner.Setup(r => r.ReadAsync("42", It.IsAny<CiReadRequest>(), It.IsAny<CancellationToken>())).ReturnsAsync(state);
}
