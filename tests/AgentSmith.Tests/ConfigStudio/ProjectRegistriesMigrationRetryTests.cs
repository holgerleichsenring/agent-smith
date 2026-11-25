using AgentSmith.Infrastructure.Core.Services.Configuration.Studio;
using AgentSmith.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using static AgentSmith.Tests.ConfigStudio.ProjectRegistriesMigrationFixture;

namespace AgentSmith.Tests.ConfigStudio;

/// <summary>2026-11-25-9992d: what the migration does not finish is left for the next migrate, and nothing is lost on
/// the way — stored feeds no project can receive stay where they are.</summary>
public sealed class ProjectRegistriesMigrationRetryTests
{
    [Fact]
    public void Move_FeedsButNoProject_KeepsTheSingletonAndIsNotComplete()
    {
        using var harness = Seeded(withProjects: false);

        Mover(harness).Move(harness.DocStore).Should().BeNull("there is nowhere to put the feeds yet");
        Mover(harness).TryMove(harness.DocStore).Should().BeFalse("an incomplete move is not recorded");

        harness.DocStore.LoadAll().Should().ContainSingle(r => r.Type == ConfigDocTypes.Registries)
            .Which.Doc.Should().Be(Feeds);
    }

    [Fact]
    public async Task RunAsync_StoreUnreachable_IsNotCompleteSoTheNextMigrateTriesAgain()
    {
        var options = new DbContextOptionsBuilder<AgentSmithDbContext>().UseSqlite("Data Source=:memory:").Options;
        await using var db = new AgentSmithDbContext(options);

        (await Mover(new DbConfigTestHarness()).RunAsync(db, CancellationToken.None)).Should().BeFalse();
    }
}
