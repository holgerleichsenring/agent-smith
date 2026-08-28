using FluentAssertions;

namespace AgentSmith.PipelineHarness.DataToolchain;

/// <summary>
/// 2026-08-28-c5e7: the pinned catalog carries domain profiles at all. Every other
/// profile assertion in this project is ARMED at the pin and returns early below it,
/// which is right for a gate over content that may not have shipped yet — and leaves
/// nothing that fails when a later pin drops <c>profiles/</c> again. This is that
/// assertion, and it is deliberately unconditional.
/// </summary>
public sealed class EmbeddedProfileCatalogTests : IDisposable
{
    private const string ShippedDomain = "dbt-databricks";

    private readonly PackagedProfiles _profiles = new();

    [Fact]
    public void EmbeddedCatalog_KnownDomains_CarriesTheShippedProfile()
    {
        _profiles.Armed.Should().BeTrue(
            $"the embedded pin is {_profiles.Pin} and profiles ship from "
            + $"{PackagedProfiles.ProfilesFrom} — below that, every domain a repository "
            + "declares is refused as unknown before any sandbox is created");

        _profiles.KnownDomains().Should().Contain(ShippedDomain,
            "a run resolves a declared domain against the catalog the binary embeds, "
            + "so a pin that carries no profile makes a correct declaration and a typo "
            + "indistinguishable");
    }

    public void Dispose() => _profiles.Dispose();
}
