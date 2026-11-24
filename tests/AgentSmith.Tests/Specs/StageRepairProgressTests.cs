using AgentSmith.Application.Services.Handlers;
using AgentSmith.Application.Services.Specs;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Models;
using FluentAssertions;

namespace AgentSmith.Tests.Specs;

/// <summary>2026-11-24-6811c: the one rule whether a red stage may go back to a master again — the repair wrote, and the
/// failure it left is another one — shared by a spec's repair and the refresh-base run's.</summary>
public sealed class StageRepairProgressTests
{
    private static readonly VerifyOutcome Red = new("server", "test", "dotnet test", 1, false, "NETSDK1087 duplicate reference");
    private static readonly IReadOnlyList<CodeChange> Wrote = [new(new FilePath("a.csproj"), "<Project/>", "Modify")];

    [Fact]
    public void MadeProgress_TheSameRedAgain_OrNothingWritten_IsNone()
    {
        var signature = StageRepairProgress.Signature(Red);
        var other = StageRepairProgress.Signature(Red with { Output = "another failure" });

        StageRepairProgress.MadeProgress(signature, signature, Wrote).Should().BeFalse();
        StageRepairProgress.MadeProgress(signature, other, []).Should().BeFalse();
        StageRepairProgress.MadeProgress(signature, other, null).Should().BeFalse();
        StageRepairProgress.MadeProgress(signature, other, Wrote).Should().BeTrue();
    }
}
