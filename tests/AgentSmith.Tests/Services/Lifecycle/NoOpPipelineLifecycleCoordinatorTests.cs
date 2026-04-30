using AgentSmith.Application.Services.Lifecycle;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using FluentAssertions;

namespace AgentSmith.Tests.Services.Lifecycle;

public sealed class NoOpPipelineLifecycleCoordinatorTests
{
    [Fact]
    public async Task BeginAsync_ReturnsDisposableThatDoesNothing()
    {
        var sut = new NoOpPipelineLifecycleCoordinator();

        var scope = await sut.BeginAsync(new ProjectConfig(), new PipelineContext(), CancellationToken.None);

        var act = async () =>
        {
            scope.MarkFailed();
            await scope.DisposeAsync();
        };
        await act.Should().NotThrowAsync();
    }
}
