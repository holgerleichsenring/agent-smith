using AgentSmith.Application.Services.Sandbox;
using AgentSmith.Contracts.Models.Skills;
using FluentAssertions;

namespace AgentSmith.PipelineHarness.DataToolchain;

/// <summary>
/// p0513: the profile may declare only what was measured. p0505's table is the
/// evidence; this is the gate that makes declaring past it impossible.
/// </summary>
public sealed class DataProfileGateTests : IDisposable
{
    private const string Domain = "dbt-databricks";
    private const string BundleSchemaCheck = "check-jsonschema";

    private readonly PackagedProfiles _profiles = new();
    private readonly MeasuredCommandsSource _source = new();
    private readonly MeasuredCommandGate _gate = new();

    // 2026-08-28-3302: one guard, and it asserts. This used to arm at the pin and
    // return null below it, and every caller then returned early — an assertion that
    // does not run reads exactly like one that holds.
    private DomainProfile Profile()
    {
        var profile = _profiles.Find(Domain);
        profile.Should().NotBeNull(
            $"the embedded pin is {_profiles.Pin} and profiles ship from "
            + $"{PackagedProfiles.ProfilesFrom} — a pin below that carries no profile to police");
        return profile!;
    }

    [Fact]
    public void Profile_Data_DeclaresOnlyCommandsTheMeasuredTableRecordsAsDeclarable()
    {
        var profile = Profile();

        _gate.Undeclarable(profile, _source.Load()).Should().BeEmpty(
            $"every command '{Domain}' declares must be a row this repository measured as "
            + "declarable — an unmeasured command is a guess, and a guess is a red build "
            + "on somebody else's clean repository");
    }

    [Fact]
    public void Profile_WithNoMeasuredTable_IsNotGatedByTheTable()
    {
        var unmeasured = new DomainProfile(
            "some-other-domain", "python:3.12-bookworm", [],
            [new DomainProfileCommand("build", "tool nobody-measured")]);

        _gate.Gates(unmeasured).Should().BeFalse();
        _gate.Undeclarable(unmeasured, _source.Load()).Should().BeEmpty(
            "a profile this repository offers no evidence about is not gated by evidence "
            + "it was never offered");
    }

    [Fact]
    public void Profile_Data_DeclaresNoBundleSchemaCommand()
    {
        var profile = Profile();

        profile.Verify.Should().NotContain(
            c => c.Command.Contains(BundleSchemaCheck, StringComparison.Ordinal),
            "the measured form names a schema file the harness injected and a fixture's own "
            + "file name; the form a repository could run appears in no row, so declaring it "
            + "would be the guess this gate exists to refuse");
    }

    [Fact]
    public void Profile_Data_TheImagePassesThePackagingGate()
    {
        var profile = Profile();

        // 2026-08-25-014d: the gate is the registry policy, judged with the default
        // (unconfigured) boundary — a profile ships in the binary, so it has to clear the
        // policy an operator who has configured nothing gets. The tag pattern that used to
        // stand next to this is gone: whether the image carries git is discovered at the
        // checkout that needs it, by name, instead of guessed from how the tag looks.
        new ImageRegistryTrust().Accepts(profile.Image).Should().BeTrue(
            $"'{profile.Image}' must come from a trusted registry or no sandbox starts for it");
    }

    public void Dispose() => _profiles.Dispose();
}
