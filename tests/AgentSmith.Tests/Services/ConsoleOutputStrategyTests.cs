using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Services;
using AgentSmith.Infrastructure.Services.Output;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentSmith.Tests.Services;

public sealed class ConsoleOutputStrategyTests
{
    private readonly ConsoleOutputStrategy _sut = new(NullLogger<ConsoleOutputStrategy>.Instance);

    [Fact]
    public void ProviderType_IsConsole()
    {
        _sut.ProviderType.Should().Be("console");
    }

    [Fact]
    public async Task DeliverAsync_NoFindings_CompletesWithoutError()
    {
        var context = new OutputContext("my-api", null, [], null, "./test-output", new PipelineContext());

        var act = async () => await _sut.DeliverAsync(context);

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task DeliverAsync_WithFindings_CompletesWithoutError()
    {
        var findings = new List<Finding>
        {
            new("HIGH", "src/Api/UserController.cs", 47, 52, "SQL injection", "Unsanitized input", 9),
            new("MEDIUM", "src/Auth/TokenService.cs", 23, null, "JWT secret", "No validation", 8),
            new("LOW", "src/Config/DbConfig.cs", 8, null, "Logged secret", "Connection string in log", 8),
        };

        var context = new OutputContext("my-api", "42", findings, "# Report", "./test-output", new PipelineContext());

        var act = async () => await _sut.DeliverAsync(context);

        await act.Should().NotThrowAsync();
    }
}
