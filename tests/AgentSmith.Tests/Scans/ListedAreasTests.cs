using AgentSmith.Application.Services.Tools;
using FluentAssertions;

namespace AgentSmith.Tests.Scans;

/// <summary>2026-11-09-0620k: a coverage nudge names the listed directories a review opened nothing under.</summary>
public sealed class ListedAreasTests
{
    [Fact]
    public void ListedAreas_SomeListedNeverOpened_AreNamedInnermostFirst()
    {
        var unread = ListedAreas.Unread(
            [".", "src", "src/Api", "src/Auth", ".github/workflows"], ["src/Auth/Login.cs"], new object());

        unread.Should().Equal(".github/workflows", "src/Api");
    }
}
