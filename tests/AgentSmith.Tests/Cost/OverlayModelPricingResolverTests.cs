using AgentSmith.Application.Services;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;
using FluentAssertions;

namespace AgentSmith.Tests.Cost;

/// <summary>
/// p0274: the live per-call cost path must price config-defined models (the bug
/// was $0.0000 despite real tokens because the bare resolver ignored project
/// pricing and lacked gpt-5.1). The shared OverlayModelPricingResolver layers a
/// PricingConfig over the default resolver; gpt-5.1 is now in the defaults too.
/// </summary>
public sealed class OverlayModelPricingResolverTests
{
    private static PricingConfig Config(string model, decimal input) =>
        new() { Models = { [model] = new ModelPricing { InputPerMillion = input, OutputPerMillion = input } } };

    [Fact]
    public void Resolve_ConfigModelAbsentFromDefaults_PricedFromConfig()
    {
        // A model the built-in defaults do not know — config must price it (the gpt-5.1 case).
        var sut = new OverlayModelPricingResolver(new ModelPricingResolver(), Config("acme-llm-9", 7.5m));

        sut.Resolve("acme-llm-9")!.InputPerMillion.Should().Be(7.5m);
    }

    [Fact]
    public void Resolve_OverrideForKnownModel_ConfigWinsOverDefault()
    {
        var sut = new OverlayModelPricingResolver(new ModelPricingResolver(), Config("gpt-4.1", 99m));

        sut.Resolve("gpt-4.1")!.InputPerMillion.Should().Be(99m);
    }

    [Fact]
    public void Resolve_NullConfig_DelegatesToBaseDefaults()
    {
        var sut = new OverlayModelPricingResolver(new ModelPricingResolver(), overrides: null);

        sut.Resolve("gpt-4.1")!.InputPerMillion.Should().Be(2.0m);
    }

    [Fact]
    public void Resolve_UnknownToBoth_ReturnsNull()
    {
        var sut = new OverlayModelPricingResolver(new ModelPricingResolver(), Config("acme-llm-9", 7.5m));

        sut.Resolve("totally-unknown-model").Should().BeNull();
    }

    [Theory]
    [InlineData("gpt-5.1")]
    [InlineData("gpt-5.1-2025-01-01")] // date-suffixed id resolves via longest-prefix
    public void Resolve_Gpt5Point1_PricedFromDefaults(string model)
    {
        new ModelPricingResolver().Resolve(model)!.InputPerMillion.Should().Be(1.25m);
    }
}
