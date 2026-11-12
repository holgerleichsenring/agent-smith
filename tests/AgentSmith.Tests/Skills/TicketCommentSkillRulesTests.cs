using AgentSmith.Tests.Prompts;
using FluentAssertions;

namespace AgentSmith.Tests.Skills;

/// <summary>
/// 2026-10-28-7a69d: the comment outcome, read from the PINNED catalog. The write tool and the approval path work
/// without these sentences; a release that drops them leaves a design partner that never proposes one.
/// <para>
/// 2026-11-12-681f: the comment is written with write_comment, whose parameter is the contract; the outcome block
/// the parser read is gone.
/// </para>
/// </summary>
public sealed class TicketCommentSkillRulesTests
{
    /// <summary>First agent-smith-skills release carrying the comment outcome.</summary>
    private static readonly Version CommentRelease = new(5, 22, 0);

    private static string Because(string rule) => $"the pin is {PackagedMaster.Pin} and {rule} ships from v{CommentRelease}";

    [Fact]
    public void EmbeddedCatalog_DesignPartner_DocumentsTheCommentOutcome()
    {
        var master = PackagedMaster.Folded("design-partner-master");

        master.Should().Contain("### A conversation bound to a ticket", Because("the bound-ticket review"));
        // 2026-11-11-f874: the situation (whether it holds), not a scripted first answer.
        master.Should().Contain("the person wants to know whether the ticket holds", Because("judging the ticket first"));
        master.Should().NotContain("Your first reply is an **answer**", "the bound-ticket section states no reply order");
        master.Should().Contain("`write_comment`, in a conversation bound to a ticket", Because("the comment tool"));
        master.Should().Contain("Approving it posts the comment and starts nothing.", Because("the comment outcome"));
    }
}
