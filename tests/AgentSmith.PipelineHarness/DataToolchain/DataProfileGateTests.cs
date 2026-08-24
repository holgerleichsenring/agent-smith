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

    // Below the pin the profile is not in the binary yet, so there is nothing to
    // police — but the tarball must still be readable, or this would pass because
    // the read broke rather than because the profile is clean.
    private DomainProfile? Profile()
    {
        if (_profiles.Armed) return _profiles.Find(Domain);
        _profiles.Entries().Should().NotBeEmpty(
            "the pinned catalog must stay readable while it predates profiles/");
        return null;
    }

    [Fact]
    public void Profile_Data_DeclaresOnlyCommandsTheMeasuredTableRecordsAsDeclarable()
    {
        if (Profile() is not { } profile) return;

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
        if (Profile() is not { } profile) return;

        profile.Verify.Should().NotContain(
            c => c.Command.Contains(BundleSchemaCheck, StringComparison.Ordinal),
            "the measured form names a schema file the harness injected and a fixture's own "
            + "file name; the form a repository could run appears in no row, so declaring it "
            + "would be the guess this gate exists to refuse");
    }

    [Fact]
    public void Profile_Data_TheImagePassesThePackagingGate()
    {
        if (Profile() is not { } profile) return;

        ToolchainImageCatalog.IsTrustedRegistry(profile.Image).Should().BeTrue(
            $"'{profile.Image}' must come from a trusted registry or no sandbox starts for it");
        ToolchainImageCatalog.IsGitBearing(profile.Image).Should().BeTrue(
            $"'{profile.Image}' must carry git — a sandbox runs `git clone` inside it");
    }

    public void Dispose() => _profiles.Dispose();
}
