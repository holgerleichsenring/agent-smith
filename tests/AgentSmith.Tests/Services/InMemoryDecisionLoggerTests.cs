using AgentSmith.Contracts.Decisions;
using AgentSmith.Infrastructure.Core.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentSmith.Tests.Services;

public sealed class InMemoryDecisionLoggerTests
{
    private readonly InMemoryDecisionLogger _sut = new(NullLogger<InMemoryDecisionLogger>.Instance);

    [Fact]
    public async Task LogAsync_CompletesWithoutError()
    {
        var act = async () => await _sut.LogAsync(
            null, DecisionCategory.Architecture, "**Test**: reason");

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task LogAsync_WithRepoPath_CompletesWithoutError()
    {
        var act = async () => await _sut.LogAsync(
            "/some/path", DecisionCategory.Tooling, "**Tool**: reason");

        await act.Should().NotThrowAsync();
    }
}
