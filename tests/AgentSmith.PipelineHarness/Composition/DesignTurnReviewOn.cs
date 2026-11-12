using AgentSmith.Application.Services.Specs.Review;
using AgentSmith.Contracts.Models.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Moq;

namespace AgentSmith.PipelineHarness.Composition;

/// <summary>2026-11-12-cd4ch: spec_review with the design turn's advisory pass on, which the default leaves off, for
/// a harness whose tests watch that pass.</summary>
internal static class DesignTurnReviewOn
{
    public static void Apply(IServiceCollection services)
    {
        var reader = new Mock<ISpecReviewSettingsReader>();
        reader.Setup(r => r.Current()).Returns(new SpecReviewSettings(new SpecReviewConfig { DesignTurn = true }, 1800));
        services.RemoveAll<ISpecReviewSettingsReader>();
        services.AddSingleton(reader.Object);
    }
}
