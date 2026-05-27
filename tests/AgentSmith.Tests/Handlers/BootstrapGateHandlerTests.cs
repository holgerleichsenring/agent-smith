using AgentSmith.Application.Models;
using AgentSmith.Application.Services.Handlers;
using AgentSmith.Contracts.Activation;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentSmith.Tests.Handlers;

public sealed class BootstrapGateHandlerTests
{
    private readonly BootstrapGateHandler _sut = new(
        RunStateConceptsTestFactory.Default,
        EventTestStubs.NoOp,
        NullLogger<BootstrapGateHandler>.Instance);

    [Fact]
    public async Task ExecuteAsync_BothFilesPresent_ReturnsOk()
    {
        var pipeline = PipelineFor("fix-bug",
            contextYamlPresent: true, codingPrinciplesPresent: true);

        var result = await _sut.ExecuteAsync(new BootstrapGateContext(pipeline), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task ExecuteAsync_ContextYamlMissing_ReturnsFailWithStructuredMessage()
    {
        var pipeline = PipelineFor("fix-bug",
            contextYamlPresent: false, codingPrinciplesPresent: true);

        var result = await _sut.ExecuteAsync(new BootstrapGateContext(pipeline), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Message.Should().Contain("Run init-project first");
    }

    [Fact]
    public async Task ExecuteAsync_CodingPrinciplesMissing_ReturnsFail()
    {
        var pipeline = PipelineFor("security-scan",
            contextYamlPresent: true, codingPrinciplesPresent: false);

        var result = await _sut.ExecuteAsync(new BootstrapGateContext(pipeline), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
    }

    [Fact]
    public async Task ExecuteAsync_ApiSecurityScan_PassiveMode_SkipsGate()
    {
        var pipeline = PipelineFor("api-security-scan",
            contextYamlPresent: false, codingPrinciplesPresent: false,
            sourceAvailable: false);

        var result = await _sut.ExecuteAsync(new BootstrapGateContext(pipeline), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Message.Should().Contain("passive api-scan mode");
    }

    [Fact]
    public async Task ExecuteAsync_ApiSecurityScan_SourceAvailable_FilesMissing_ReturnsFail()
    {
        var pipeline = PipelineFor("api-security-scan",
            contextYamlPresent: false, codingPrinciplesPresent: true,
            sourceAvailable: true);

        var result = await _sut.ExecuteAsync(new BootstrapGateContext(pipeline), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Message.Should().Contain("Run init-project first");
    }

    [Fact]
    public async Task ExecuteAsync_ApiSecurityScan_SourceAvailable_FilesPresent_ReturnsOk()
    {
        var pipeline = PipelineFor("api-security-scan",
            contextYamlPresent: true, codingPrinciplesPresent: true,
            sourceAvailable: true);

        var result = await _sut.ExecuteAsync(new BootstrapGateContext(pipeline), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
    }

    private static PipelineContext PipelineFor(
        string pipelineName,
        bool contextYamlPresent,
        bool codingPrinciplesPresent,
        bool sourceAvailable = false)
    {
        var pipeline = new PipelineContext();
        pipeline.Set(ContextKeys.ResolvedPipeline, new ResolvedPipelineConfig(
            pipelineName, new AgentConfig(), "skills/coding", null));

        var concepts = RunStateConceptsTestFactory.Default(pipeline);
        concepts.SetBool("context_yaml_present", contextYamlPresent);
        concepts.SetBool("coding_principles_present", codingPrinciplesPresent);
        concepts.SetBool("source_available", sourceAvailable);
        return pipeline;
    }
}
