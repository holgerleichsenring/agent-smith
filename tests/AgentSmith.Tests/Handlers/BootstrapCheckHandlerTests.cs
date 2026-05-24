using AgentSmith.Application.Models;
using AgentSmith.Application.Services.Handlers;
using AgentSmith.Contracts.Activation;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AgentSmith.Tests.Handlers;

public sealed class BootstrapCheckHandlerTests
{
    private const string ContextYamlPath = "/work/.agentsmith/contexts/default/context.yaml";
    private const string PrinciplesPath = "/work/.agentsmith/contexts/default/coding-principles.md";

    private readonly Mock<ISandboxFileReaderFactory> _readerFactoryMock = new();
    private readonly Mock<ISandboxFileReader> _readerMock = new();
    private readonly Mock<ISandbox> _sandboxMock = new();

    public BootstrapCheckHandlerTests()
    {
        _readerFactoryMock.Setup(f => f.Create(It.IsAny<ISandbox>())).Returns(_readerMock.Object);
    }

    [Fact]
    public async Task ExecuteAsync_BothFilesPresent_PublishesBothTrue()
    {
        SetupReader(contextYamlExists: true, principlesExists: true);
        var pipeline = PipelineWithSandbox();

        var result = await Handler().ExecuteAsync(new BootstrapCheckContext(pipeline), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        var concepts = RunStateConceptsTestFactory.Default(pipeline);
        concepts.GetBool("context_yaml_present").Should().BeTrue();
        concepts.GetBool("coding_principles_present").Should().BeTrue();
    }

    [Fact]
    public async Task ExecuteAsync_ContextMissing_PublishesContextFalse()
    {
        SetupReader(contextYamlExists: false, principlesExists: true);
        var pipeline = PipelineWithSandbox();

        await Handler().ExecuteAsync(new BootstrapCheckContext(pipeline), CancellationToken.None);

        var concepts = RunStateConceptsTestFactory.Default(pipeline);
        concepts.GetBool("context_yaml_present").Should().BeFalse();
        concepts.GetBool("coding_principles_present").Should().BeTrue();
    }

    [Fact]
    public async Task ExecuteAsync_PrinciplesMissing_PublishesPrinciplesFalse()
    {
        SetupReader(contextYamlExists: true, principlesExists: false);
        var pipeline = PipelineWithSandbox();

        await Handler().ExecuteAsync(new BootstrapCheckContext(pipeline), CancellationToken.None);

        var concepts = RunStateConceptsTestFactory.Default(pipeline);
        concepts.GetBool("context_yaml_present").Should().BeTrue();
        concepts.GetBool("coding_principles_present").Should().BeFalse();
    }

    [Fact]
    public async Task ExecuteAsync_NeitherFile_PublishesBothFalse()
    {
        SetupReader(contextYamlExists: false, principlesExists: false);
        var pipeline = PipelineWithSandbox();

        await Handler().ExecuteAsync(new BootstrapCheckContext(pipeline), CancellationToken.None);

        var concepts = RunStateConceptsTestFactory.Default(pipeline);
        concepts.GetBool("context_yaml_present").Should().BeFalse();
        concepts.GetBool("coding_principles_present").Should().BeFalse();
    }

    private void SetupReader(bool contextYamlExists, bool principlesExists)
    {
        _readerMock.Setup(r => r.ExistsAsync(ContextYamlPath, It.IsAny<CancellationToken>()))
            .ReturnsAsync(contextYamlExists);
        _readerMock.Setup(r => r.ExistsAsync(PrinciplesPath, It.IsAny<CancellationToken>()))
            .ReturnsAsync(principlesExists);
    }

    private PipelineContext PipelineWithSandbox()
    {
        var pipeline = new PipelineContext();
        pipeline.Set(ContextKeys.Sandbox, _sandboxMock.Object);
        pipeline.Set<IReadOnlyDictionary<string, ISandbox>>(
            ContextKeys.Sandboxes,
            new Dictionary<string, ISandbox>(StringComparer.Ordinal) { ["default"] = _sandboxMock.Object });
        pipeline.Set<IReadOnlyDictionary<string, RemoteContextDiscovery>>(
            ContextKeys.SandboxDiscoveries,
            new Dictionary<string, RemoteContextDiscovery>(StringComparer.Ordinal)
            {
                ["default"] = new RemoteContextDiscovery("default", ".", "csharp")
            });
        return pipeline;
    }

    private BootstrapCheckHandler Handler() => new(
        _readerFactoryMock.Object,
        RunStateConceptsTestFactory.Default,
        NullLogger<BootstrapCheckHandler>.Instance);
}
