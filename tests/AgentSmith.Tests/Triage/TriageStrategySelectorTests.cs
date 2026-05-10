using AgentSmith.Application.Services.Triage;
using AgentSmith.Contracts.Models.Configuration;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AgentSmith.Tests.Triage;

/// <summary>
/// p0131c: selector collapses to a one-liner returning StructuredTriageStrategy
/// for every (PipelineType, name) pair. The interface stays as a DI seam.
/// </summary>
public sealed class TriageStrategySelectorTests
{
    private readonly StructuredTriageStrategy _structured = new(
        Mock.Of<AgentSmith.Contracts.Services.ITriageOutputProducer>(),
        new PhaseCommandExpander(),
        new SinglePhaseCollapser(),
        NullLogger<StructuredTriageStrategy>.Instance);

    private TriageStrategySelector Sut() => new(_structured);

    [Theory]
    [InlineData(PipelineType.Discussion, "mad-discussion")]
    [InlineData(PipelineType.Discussion, "legal-analysis")]
    [InlineData(PipelineType.Discussion, "autonomous")]
    [InlineData(PipelineType.Discussion, "init-project")]
    [InlineData(PipelineType.Discussion, "skill-manager")]
    [InlineData(PipelineType.Hierarchical, "fix-bug")]
    [InlineData(PipelineType.Hierarchical, "add-feature")]
    [InlineData(PipelineType.Structured, "security-scan")]
    [InlineData(PipelineType.Structured, "api-security-scan")]
    public void Select_AnyType_ReturnsStructured(PipelineType type, string name) =>
        Sut().Select(type, name).Should().BeSameAs(_structured);
}
