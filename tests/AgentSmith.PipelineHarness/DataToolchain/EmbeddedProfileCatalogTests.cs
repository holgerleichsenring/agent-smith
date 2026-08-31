using FluentAssertions;

namespace AgentSmith.PipelineHarness.DataToolchain;

/// <summary>
/// 2026-08-28-c5e7: the pinned catalog carries domain profiles at all. Every other
/// profile assertion in this project used to be ARMED at the pin and return early
/// below it, which left nothing that fails when a later pin drops <c>profiles/</c>
/// again. 2026-08-28-3302 removed that arming everywhere; this is the assertion the
/// removal rests on, and it was always unconditional.
/// </summary>
public sealed class EmbeddedProfileCatalogTests : IDisposable
{
    private const string ShippedDomain = "dbt-databricks";

    private readonly PackagedProfiles _profiles = new();

    [Fact]
    public void EmbeddedCatalog_KnownDomains_CarriesTheShippedProfile()
    {
        _profiles.KnownDomains().Should().Contain(ShippedDomain,
            $"the embedded pin is {_profiles.Pin} and profiles ship from "
            + $"{PackagedProfiles.ProfilesFrom} — a run resolves a declared domain against "
            + "the catalog the binary embeds, so a pin below that floor makes a correct "
            + "declaration and a typo indistinguishable: both are refused as unknown");
    }

    public void Dispose() => _profiles.Dispose();
}
