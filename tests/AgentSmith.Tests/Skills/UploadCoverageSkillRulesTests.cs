using AgentSmith.Tests.Prompts;
using FluentAssertions;

namespace AgentSmith.Tests.Skills;

/// <summary>
/// 2026-11-04-96a0c: what earlier turns read of an upload, read from the PINNED catalog. The prompt states how many
/// readable files earlier turns opened under each address and that their content is not in this turn.
/// <para>
/// 2026-11-12-681f: read_notes/write_notes replaced note_reference (2026-11-12-cd4ck); the master tells the turn to
/// keep in its notes what the next turn should not have to re-read.
/// </para>
/// </summary>
public sealed class UploadCoverageSkillRulesTests
{
    /// <summary>First agent-smith-skills release carrying the notes paragraph (design-partner-master 3.0.0).</summary>
    private static readonly Version NotesRelease = new(5, 29, 0);

    private static string Because(string rule) => $"the pin is {PackagedMaster.Pin} and {rule} ships from v{NotesRelease}";

    [Fact]
    public void EmbeddedCatalog_DesignPartner_KeepsWhatTheNextTurnNeedsInItsNotes()
    {
        var master = PackagedMaster.Folded("design-partner-master");

        master.Should().Contain("`read_notes` and `write_notes` keep this conversation's working file",
            Because("the notes tools"));
        master.Should().Contain("Keep there what the next turn should not have to re-read", Because("what the notes are for"));
        master.Should().Contain("`write_notes` replaces the file with the whole text you pass", Because("the write's effect"));
        master.Should().NotContain("note_reference", "the per-upload note is gone");
    }
}
