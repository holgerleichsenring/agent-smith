using AgentSmith.Application.Services.Handlers;
using AgentSmith.Domain.Models;
using FluentAssertions;

namespace AgentSmith.Tests.Handlers;

/// <summary>
/// p0422: the master must be told what the framework provisioned for it.
/// <para>
/// Run 22 skipped every private-feed package and wrote "Private Azure Artifacts feed — no
/// credentials in sandbox" into its own decisions.md. It had never tried: zero mentions
/// of the packages, zero 401s in the whole run. The credentials WERE staged and the build
/// had already used them. An agent that cannot see its own provisioning invents a reason
/// for the work it skips — and the reason lands in the delivery artefact.
/// </para>
/// </summary>
public sealed class StagedRegistriesInWorkingStateTests
{
    [Fact]
    public void TheWorkingState_NamesTheCredentialsTheFrameworkStaged()
    {
        var state = WorkingStateSection.Build(
            [], null, changedPaths: [],
            // Reported from what was WRITTEN — a .NET config for a .NET repo, an .npmrc for
            // an npm one. The framework never names an ecosystem the repo does not use.
            stagedRegistries: [
                "api: /root/.nuget/NuGet/NuGet.Config — pkgs.example.test",
                "web: /root/.npmrc — registry.example.test",
                "engine: /root/.cargo/credentials.toml — staged by the generic path",
                "service: /root/.m2/settings.xml — staged by the generic path",
            ]);

        state.Should().Contain("Package-feed credentials the framework staged for you");
        state.Should().Contain("pkgs.example.test");
        state.Should().Contain("/root/.npmrc — registry.example.test",
            "each ecosystem is told where ITS credentials went, never another's");
        state.Should().Contain("/root/.cargo/credentials.toml");
        state.Should().Contain("/root/.m2/settings.xml",
            "cargo, maven, pip and go are staged by the generic path — the two fast paths "
            + "are an optimisation, not the list of ecosystems that exist");
        state.Should().Contain("never record that credentials are absent without trying",
            "the failure mode is a plausible reason invented for skipped work");
    }

    [Fact]
    public void WithNothingStaged_TheBlockSaysNothing()
    {
        var state = WorkingStateSection.Build([], null, changedPaths: []);

        state.Should().NotContain("Package-feed credentials",
            "a run with no private feed should not be told about one");
    }
}
