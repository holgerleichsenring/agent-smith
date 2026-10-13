using AgentSmith.Contracts.Models.Structure;
using AgentSmith.Contracts.Services;
using AgentSmith.Infrastructure.Services.Structure;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;

namespace AgentSmith.Tests.Structure;

public sealed class StructureEngineRegistrationTests
{
    [Fact]
    public void AddStructureEngine_ResolvedFromTheContainer_MeasuresRust()
    {
        using var provider = new ServiceCollection().AddLogging().AddStructureEngine().BuildServiceProvider();

        var result = provider.GetRequiredService<IStructureEngine>().Measure("src/lib.rs", "fn a() {}\n");

        result.Outcome.Should().Be(StructureOutcome.Measured);
        result.Units.Should().ContainSingle(u => u.Key == "a()");
    }
}
