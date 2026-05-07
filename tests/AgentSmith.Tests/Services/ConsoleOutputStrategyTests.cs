using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Services;
using AgentSmith.Infrastructure.Services.Output;
using AgentSmith.Tests.TestHelpers;
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
    public async Task DeliverAsync_NoObservations_CompletesWithoutError()
    {
        var context = new OutputContext("my-api", null, [], null, "./test-output", new PipelineContext());

        var act = async () => await _sut.DeliverAsync(context);

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task DeliverAsync_WithObservations_CompletesWithoutError()
    {
        var observations = new List<SkillObservation>
        {
            ObservationFactory.Make("HIGH", "src/Api/UserController.cs", 47, "SQL injection", "Unsanitized input", 90),
            ObservationFactory.Make("MEDIUM", "src/Auth/TokenService.cs", 23, "JWT secret", "No validation", 80),
            ObservationFactory.Make("LOW", "src/Config/DbConfig.cs", 8, "Logged secret", "Connection string in log", 80),
        };

        var context = new OutputContext("my-api", "42", observations, "# Report", "./test-output", new PipelineContext());

        var act = async () => await _sut.DeliverAsync(context);

        await act.Should().NotThrowAsync();
    }
}
