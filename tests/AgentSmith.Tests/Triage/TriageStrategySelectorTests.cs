using AgentSmith.Application.Services.Triage;
using AgentSmith.Contracts.Models.Configuration;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AgentSmith.Tests.Triage;

/// <summary>
/// p0131c-pre: TriageStrategySelector now receives (PipelineType, pipelineName)
/// so Discussion-type presets can route per-name. mad-discussion + legal-analysis
/// → Structured; autonomous → Legacy (skill-set gap, separate concern).
/// </summary>
public sealed class TriageStrategySelectorTests
{
    private readonly LegacyTriageStrategy _legacy = new(
        Mock.Of<AgentSmith.Contracts.Services.IPromptCatalog>(),
        Mock.Of<AgentSmith.Contracts.Services.IChatClientFactory>(),
        NullLogger<LegacyTriageStrategy>.Instance);

    private readonly StructuredTriageStrategy _structured = new(
        Mock.Of<AgentSmith.Contracts.Services.ITriageOutputProducer>(),
        new PhaseCommandExpander(),
        new SinglePhaseCollapser(),
        NullLogger<StructuredTriageStrategy>.Instance);

    private TriageStrategySelector Sut() => new(_legacy, _structured);

    [Theory]
    [InlineData("mad-discussion")]
    [InlineData("legal-analysis")]
    public void Select_DiscussionWithStructuredAllowedName_ReturnsStructured(string name) =>
        Sut().Select(PipelineType.Discussion, name).Should().BeSameAs(_structured);

    [Theory]
    [InlineData("autonomous")]
    [InlineData("init-project")]
    [InlineData("skill-manager")]
    public void Select_DiscussionWithoutStructuredAllowance_ReturnsLegacy(string name) =>
        Sut().Select(PipelineType.Discussion, name).Should().BeSameAs(_legacy);

    [Theory]
    [InlineData(PipelineType.Hierarchical, "fix-bug")]
    [InlineData(PipelineType.Hierarchical, "add-feature")]
    [InlineData(PipelineType.Structured, "security-scan")]
    [InlineData(PipelineType.Structured, "api-security-scan")]
    public void Select_NonDiscussion_AlwaysReturnsStructured(PipelineType type, string name) =>
        Sut().Select(type, name).Should().BeSameAs(_structured);

    [Fact]
    public void Select_NameMatchIsCaseInsensitive() =>
        Sut().Select(PipelineType.Discussion, "MAD-DISCUSSION").Should().BeSameAs(_structured);
}
