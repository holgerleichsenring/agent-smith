using AgentSmith.Infrastructure.Core.Services.Configuration.Studio;
using FluentAssertions;
using static AgentSmith.Tests.ConfigStudio.ProjectRegistriesMigrationFixture;

namespace AgentSmith.Tests.ConfigStudio;

/// <summary>
/// 2026-11-25-9992d: the installation-wide registries singleton is no longer assembled, so the feed hosts and token
/// references an operator stored there move onto the projects once — every project without feeds of its own gets a
/// copy as a new version, a project with its own keeps them, and the singleton goes.
/// </summary>
public sealed class ProjectRegistriesMigrationTests
{
    [Fact]
    public void Move_TheSingleton_IsCopiedIntoEveryProjectWithoutItsOwn_ThenRemoved()
    {
        using var harness = Seeded();

        Mover(harness).Move(harness.DocStore).Should().Be(1, "only the project without feeds of its own takes them");

        var bare = ProjectJson(harness, "bare").GetProperty("Registries");
        bare.GetArrayLength().Should().Be(2);
        bare[0].GetProperty("Host").GetString().Should().Be("pkgs.example.com");
        bare[0].GetProperty("Token").GetString().Should().Be("${feed_token}", "the secret reference moves, never a value");
        bare[1].GetProperty("Username").GetString().Should().Be("ci");
        Version(harness, "bare").Should().Be(2, "the copy is a new version of the project's document");
        harness.DocStore.LoadAll().Should().NotContain(r => r.Type == ConfigDocTypes.Registries);
    }

    [Fact]
    public void Move_AProjectWithItsOwnFeeds_KeepsThem()
    {
        using var harness = Seeded();

        Mover(harness).Move(harness.DocStore);

        var own = ProjectJson(harness, "own").GetProperty("Registries");
        own.GetArrayLength().Should().Be(1);
        own[0].GetProperty("Host").GetString().Should().Be("own.example.com");
        Version(harness, "own").Should().Be(1, "a project that keeps its list is not re-saved");
    }

    [Fact]
    public void Move_RunTwice_WritesNothingTheSecondTime()
    {
        using var harness = Seeded();

        Mover(harness).Move(harness.DocStore);

        Mover(harness).Move(harness.DocStore).Should().Be(0);
        Version(harness, "bare").Should().Be(2);
    }

    [Fact]
    public void Move_TheMovedFeeds_ReachTheProjectThroughTheStore()
    {
        using var harness = Seeded();

        Mover(harness).Move(harness.DocStore);
        harness.Store.Load();

        harness.Store.GetProjects().Single(p => p.Id == "bare").Registries!
            .Select(r => r.Host).Should().Equal("pkgs.example.com", "npm.example.com");
    }
}
