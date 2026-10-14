using AgentSmith.Infrastructure.Persistence.Entities;
using AgentSmith.Server.Services.Events;
using FluentAssertions;

namespace AgentSmith.Tests.Services.Events;

/// <summary>2026-10-13-9a59a: the REST run snapshot names the run row's project and tracker platform.</summary>
public sealed class RunSnapshotProjectTests
{
    [Fact]
    public void RunSnapshotMapper_CarriesProjectAndPlatform()
    {
        var run = new Run { Id = "2026-10-13T09-00-00-0001", Project = "alpha", Platform = "github", Pipeline = "code", Status = "running" };

        var snapshot = RunSnapshotMapper.ToSnapshot(run);

        snapshot.Project.Should().Be("alpha");
        snapshot.Platform.Should().Be("github");
    }
}
