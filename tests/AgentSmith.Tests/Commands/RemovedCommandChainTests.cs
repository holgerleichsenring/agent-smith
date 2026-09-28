using AgentSmith.Application.Services;
using AgentSmith.Contracts.Commands;
using FluentAssertions;

namespace AgentSmith.Tests.Commands;

/// <summary>
/// The expectation negotiation and the knowledge query had handlers, contexts and builders long
/// after no preset ran them. A command name with a handler and no caller reads as a capability
/// the product has; these pin that the chains are gone whole.
/// </summary>
public sealed class RemovedCommandChainTests
{
    [Theory]
    [InlineData("NegotiateExpectation")]
    [InlineData("QueryKnowledge")]
    public void CodePreset_NoHandlerRegisteredForNegotiateExpectation(string command)
    {
        typeof(CommandNames).GetFields().Select(f => f.Name).Should().NotContain(command);

        var applicationTypes = typeof(ExecutePipelineUseCase).Assembly.GetTypes().Select(t => t.Name);
        applicationTypes.Should().NotContain(
            [$"{command}Handler", $"{command}Context", $"{command}ContextBuilder"]);

        PipelinePresets.TryResolve(PipelinePresets.CodeName).Should()
            .NotContain(c => c.Contains(command, StringComparison.Ordinal));
    }
}
