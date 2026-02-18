using AgentSmith.Application.Commands;
using AgentSmith.Contracts.Commands;
using AgentSmith.Domain.ValueObjects;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AgentSmith.Tests.Commands;

public sealed record TestCommandContext(string Value) : ICommandContext;

public class CommandExecutorTests
{
    private readonly ILogger<CommandExecutor> _logger =
        NullLoggerFactory.Instance.CreateLogger<CommandExecutor>();

    [Fact]
    public async Task ExecuteAsync_WithRegisteredHandler_ReturnsSuccess()
    {
        var handler = new Mock<ICommandHandler<TestCommandContext>>();
        handler.Setup(h => h.ExecuteAsync(It.IsAny<TestCommandContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CommandResult.Ok("done"));

        var sp = BuildServiceProvider(handler.Object);
        var executor = new CommandExecutor(sp, _logger);

        var result = await executor.ExecuteAsync(new TestCommandContext("test"));

        result.Success.Should().BeTrue();
        result.Message.Should().Be("done");
    }

    [Fact]
    public async Task ExecuteAsync_WithNoHandler_ReturnsFailure()
    {
        var sp = new ServiceCollection().BuildServiceProvider();
        var executor = new CommandExecutor(sp, _logger);

        var result = await executor.ExecuteAsync(new TestCommandContext("test"));

        result.Success.Should().BeFalse();
        result.Message.Should().Contain("No handler registered");
    }

    [Fact]
    public async Task ExecuteAsync_HandlerThrows_ReturnsFailureWithException()
    {
        var handler = new Mock<ICommandHandler<TestCommandContext>>();
        handler.Setup(h => h.ExecuteAsync(It.IsAny<TestCommandContext>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("boom"));

        var sp = BuildServiceProvider(handler.Object);
        var executor = new CommandExecutor(sp, _logger);

        var result = await executor.ExecuteAsync(new TestCommandContext("test"));

        result.Success.Should().BeFalse();
        result.Message.Should().Be("boom");
        result.Exception.Should().BeOfType<InvalidOperationException>();
    }

    [Fact]
    public async Task ExecuteAsync_HandlerReturnsFail_ReturnsFailure()
    {
        var handler = new Mock<ICommandHandler<TestCommandContext>>();
        handler.Setup(h => h.ExecuteAsync(It.IsAny<TestCommandContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CommandResult.Fail("rejected"));

        var sp = BuildServiceProvider(handler.Object);
        var executor = new CommandExecutor(sp, _logger);

        var result = await executor.ExecuteAsync(new TestCommandContext("test"));

        result.Success.Should().BeFalse();
        result.Message.Should().Be("rejected");
    }

    private static ServiceProvider BuildServiceProvider(ICommandHandler<TestCommandContext> handler)
    {
        var services = new ServiceCollection();
        services.AddSingleton(handler);
        return services.BuildServiceProvider();
    }
}
