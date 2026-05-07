using AgentSmith.Contracts.Providers;
using FluentAssertions;

namespace AgentSmith.Tests.Providers;

public sealed class PrDiffProviderTests
{
    [Fact]
    public void PrDiff_Record_HoldsValues()
    {
        var files = new List<ChangedFile>
        {
            new("src/Api/Controller.cs", "@@ -1,3 +1,5 @@\n+new line", ChangeKind.Modified),
            new("src/NewFile.cs", "@@ -0,0 +1,10 @@\n+content", ChangeKind.Added),
        };

        var diff = new PrDiff("abc123", "def456", files);

        diff.BaseSha.Should().Be("abc123");
        diff.HeadSha.Should().Be("def456");
        diff.Files.Should().HaveCount(2);
        diff.Files[0].Kind.Should().Be(ChangeKind.Modified);
        diff.Files[1].Kind.Should().Be(ChangeKind.Added);
    }

    [Fact]
    public void SkillObservation_Record_HoldsValues()
    {
        var obs = AgentSmith.Tests.TestHelpers.ObservationFactory.Make(
            "HIGH", "src/Api/Controller.cs", 47,
            "SQL injection", "Unsanitized user input in query", 90);

        obs.Severity.Should().Be(AgentSmith.Contracts.Models.ObservationSeverity.High);
        obs.File.Should().Be("src/Api/Controller.cs");
        obs.StartLine.Should().Be(47);
        obs.Confidence.Should().Be(90);
    }
}
