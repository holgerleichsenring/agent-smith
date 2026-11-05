using AgentSmith.Tests.Prompts;
using FluentAssertions;

namespace AgentSmith.Tests.Skills;

/// <summary>
/// 2026-11-04-96a0c: the upload coverage rule, read from the PINNED catalog. The prompt states how many
/// readable files earlier turns opened under each address; a master paragraph that still only asks how to
/// run an upload contradicts that line and the reframed note_reference description in the same prompt.
/// </summary>
public sealed class UploadCoverageSkillRulesTests
{
    /// <summary>First agent-smith-skills release carrying the coverage paragraph (design-partner-master 1.20.0).</summary>
    private static readonly Version CoverageRelease = new(5, 23, 0);

    private static string Because(string rule) => $"the pin is {PackagedMaster.Pin} and {rule} ships from v{CoverageRelease}";

    [Fact]
    public void EmbeddedCatalog_DesignPartner_NamesTheCoverageLine()
    {
        var master = PackagedMaster.Read("design-partner-master");

        master.Should().Contain("Under each address the prompt says how many of the files", Because("pointing at the coverage line"));
        master.Should().Contain("read: an answer or a proposal built on part of an upload names that", Because("naming the part read"));
        master.Should().Contain("the system keeps the list of files opened, so the", Because("the note without a file inventory"));
    }
}
