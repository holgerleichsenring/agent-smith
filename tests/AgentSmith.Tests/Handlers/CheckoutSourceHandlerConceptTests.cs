using AgentSmith.Application.Models;
using AgentSmith.Application.Services.Handlers;
using AgentSmith.Contracts.Activation;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Models;
using AgentSmith.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AgentSmith.Tests.Handlers;

public sealed class CheckoutSourceHandlerConceptTests
{
    private readonly Mock<ISourceProviderFactory> _factoryMock = new();
    private readonly Mock<IHostSourceCloner> _clonerMock = new();

    private CheckoutSourceHandler CheckoutHandler() => new(
        _factoryMock.Object,
        RunStateConceptsTestFactory.Default,
        NullLogger<CheckoutSourceHandler>.Instance);

    private TryCheckoutSourceHandler TryCheckoutHandler() => new(
        _clonerMock.Object,
        RunStateConceptsTestFactory.Default,
        NullLogger<TryCheckoutSourceHandler>.Instance);

    [Fact]
    public async Task ExecuteAsync_CheckoutSucceeds_PublishesSourceAvailableTrue()
    {
        var providerMock = new Mock<ISourceProvider>();
        providerMock.SetupGet(p => p.ProviderType).Returns("Local");
        providerMock.Setup(p => p.CheckoutAsync(It.IsAny<BranchName>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Repository(new BranchName("main"), "/tmp/repo"));
        _factoryMock.Setup(f => f.Create(It.IsAny<RepoConnection>())).Returns(providerMock.Object);

        var pipeline = new PipelineContext();
        var context = new CheckoutSourceContext(
            new[] { new RepoConnection { Type = RepoType.Local, Path = "/tmp" } }, new BranchName("main"), pipeline);

        var result = await CheckoutHandler().ExecuteAsync(context, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        RunStateConceptsTestFactory.Default(pipeline).GetBool("source_available").Should().BeTrue();
    }

    [Fact]
    public async Task ExecuteAsync_CheckoutFails_PublishesSourceAvailableFalse()
    {
        var providerMock = new Mock<ISourceProvider>();
        providerMock.SetupGet(p => p.ProviderType).Returns("github");
        providerMock.Setup(p => p.CheckoutAsync(It.IsAny<BranchName>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Repository(new BranchName("main"), "https://example.com/x.git"));
        _factoryMock.Setup(f => f.Create(It.IsAny<RepoConnection>())).Returns(providerMock.Object);

        var pipeline = new PipelineContext();
        // No sandbox set => Fail path; URL non-empty so we hit the sandbox guard.
        var context = new CheckoutSourceContext(
            new[] { new RepoConnection { Type = RepoType.GitHub, Url = "https://example.com/x.git" } },
            new BranchName("main"), pipeline);

        var result = await CheckoutHandler().ExecuteAsync(context, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        RunStateConceptsTestFactory.Default(pipeline).GetBool("source_available").Should().BeFalse();
    }

    [Fact]
    public async Task ExecuteAsync_TryCheckoutSucceeds_PublishesSourceAvailableTrue()
    {
        var temp = Path.Combine(Path.GetTempPath(), "src-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(temp);
        try
        {
            var pipeline = new PipelineContext();
            var context = new TryCheckoutSourceContext(
                new[] { new RepoConnection { Type = RepoType.Local, Path = temp } }, null, pipeline);

            var result = await TryCheckoutHandler().ExecuteAsync(context, CancellationToken.None);

            result.IsSuccess.Should().BeTrue();
            RunStateConceptsTestFactory.Default(pipeline).GetBool("source_available").Should().BeTrue();
        }
        finally
        {
            try { Directory.Delete(temp, recursive: true); } catch { }
        }
    }

    [Fact]
    public async Task ExecuteAsync_TryCheckoutFailsSoft_PublishesSourceAvailableFalse()
    {
        var pipeline = new PipelineContext();
        var context = new TryCheckoutSourceContext(new[] { new RepoConnection() }, null, pipeline);

        var result = await TryCheckoutHandler().ExecuteAsync(context, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        RunStateConceptsTestFactory.Default(pipeline).GetBool("source_available").Should().BeFalse();
    }
}
