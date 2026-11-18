using AgentSmith.Application.Services.Specs.Review;
using AgentSmith.Contracts.Events;
using AgentSmith.Contracts.Specs;
using FluentAssertions;

namespace AgentSmith.Tests.Specs.Review;

/// <summary>2026-11-18-c8e2e: which cuts add — the replacement holds the whole quote and more.</summary>
public sealed class SpecCutAdditionTests
{
    private static SpecReviewCut Cut(string quote, string? replacement) =>
        new(SpecRegisterNames.Steps, quote, replacement, "shop/a:1 [A1]");

    [Fact]
    public void Adds_TheQuotePlusMore_Adds() =>
        SpecCutAddition.Adds(Cut("Document the order endpoints", "Document the order endpoints in an OpenAPI matrix")).Should().BeTrue();

    [Theory]
    [InlineData(null)]
    [InlineData("Document the order endpoint")]
    [InlineData("List the two order routes in the README")]
    [InlineData("Document the order endpoints")]
    public void Adds_ARemovalANarrowingRewriteOrTheSameWords_DoesNotAdd(string? replacement) =>
        SpecCutAddition.Adds(Cut("Document the order endpoints", replacement)).Should().BeFalse();
}
