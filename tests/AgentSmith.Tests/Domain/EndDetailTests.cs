using AgentSmith.Domain.Models;
using FluentAssertions;

namespace AgentSmith.Tests.Domain;

/// <summary>
/// 2026-11-23-31dca: a bootstrap refusal records each repository's parent, since a stacked run's repos are cut from different
/// branches; a detail recorded before that names one parent for every repository and still reads.
/// </summary>
public sealed class EndDetailTests
{
    [Fact]
    public void ParentOf_ARepoWithAnEntry_ReadsItsOwn_AnotherTheSharedParent()
    {
        var detail = new EndDetail("s", ["server", "client"], ParentTicketId: "40",
            ParentsPerRepo: new Dictionary<string, string> { ["server"] = "41" });

        detail.ParentOf("server").Should().Be("41");
        detail.ParentOf("client").Should().Be("40");
    }

    [Fact]
    public void ParentOf_ADetailRecordedBefore_NamesItsOneParent() =>
        new EndDetail("s", ["server"], ParentTicketId: "40").ParentOf("server").Should().Be("40");
}
