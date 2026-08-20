using AgentSmith.Infrastructure.Core.Services.Skills;
using FluentAssertions;

namespace AgentSmith.Tests.Prompts;

/// <summary>
/// p0486: a live migration run built two repositories, passed 226 + 2,009 tests and opened
/// its pull requests; the operator started both hosts and confirmed the migration works. The
/// gate refused it because the inventory document "claims 35 request handlers … and its
/// listed names do not total 35". The ticket had asked to run the greps and CAPTURE THE
/// RESULTS; the agent ran every one of them and then retyped them into prose.
/// <para>
/// A model that retypes a list shortens it — summarising is what reading is for. So the
/// master redirects the command into the artifact instead of copying it, and a number in
/// such a document stands on the entries beneath it.
/// </para>
/// <para>
/// Authored in agent-smith-skills and reaching this repository only through
/// <c>SkillsCatalogVersion</c>, so the guard is written against the PACKAGED master and gated
/// on the pin, in the pattern p0412 established: below the arming release it states the gap
/// out loud, at or above it the assertions go live with no second commit. This pins that the
/// master is TOLD; whether a run then redirects rather than retypes is visible only in a live
/// run.
/// </para>
/// </summary>
public sealed class CodingMasterArtifactCapturePromptTests
{
    /// <summary>First agent-smith-skills release carrying the p0486 rule.</summary>
    private static readonly Version ArtifactCaptureRelease = new(4, 6, 0);

    private const string CaptureRule =
        "**When such an artifact captures what a command found, let the command write";

    private const string CountRule = "the entries it counts stand beneath it";

    private const string RepoRelative = "the target is repo-relative";

    private static (string Master, bool Armed) Packaged()
    {
        var catalog = new EmbeddedSkillsCatalog();
        var master = PackagedMaster.Read(catalog, "coding-agent-master");
        return (master, ParsePin(catalog.Version) >= ArtifactCaptureRelease);
    }

    [Fact]
    public void PackagedCodingMaster_CapturedOutput_IsRedirectedNotRetyped()
    {
        var (master, armed) = Packaged();

        if (armed)
        {
            master.Should().Contain(CaptureRule,
                "an inventory assembled by retyping a search's results is a second claim about them");
            return;
        }

        master.Should().NotContain(CaptureRule,
            $"the pinned catalog predates the p0486 skills release (v{ArtifactCaptureRelease}) "
            + "— raise SkillsCatalogVersion to it and this assertion goes live");
    }

    [Fact]
    public void PackagedCodingMaster_ACountInAnArtifact_StandsOnItsList()
    {
        var (master, armed) = Packaged();

        if (armed)
        {
            master.Should().Contain(CountRule,
                "a number written beside a list is a second claim, and the two drift");
            return;
        }

        master.Should().NotContain(CountRule);
    }

    /// <summary>run_command runs the shell INSIDE the repository it is given, while write_file
    /// takes a repo-prefixed path. A rule that only said "redirect it" would produce
    /// repo/repo/… on the first attempt.</summary>
    [Fact]
    public void PackagedCodingMaster_TheRedirectTarget_IsRepoRelative()
    {
        var (master, armed) = Packaged();

        if (armed)
        {
            master.Should().Contain(RepoRelative);
            return;
        }

        master.Should().NotContain(RepoRelative);
    }

    /// <summary>The new rule lands beside the artifact-phase paragraph, not in place of it —
    /// a claim that only means anything once the pin carries both, since that paragraph is
    /// itself newer than the currently pinned release.</summary>
    [Fact]
    public void PackagedCodingMaster_TheArtifactPhaseParagraph_IsUnchanged()
    {
        var (master, armed) = Packaged();
        if (!armed) return;

        master.Should().Contain(
            "whose completion criteria are met by an inventory, a report or a decision",
            "the capture rule is added beside this paragraph, never in place of it");
    }

    private static Version ParsePin(string version) =>
        Version.TryParse(version.TrimStart('v'), out var parsed) ? parsed : new Version(0, 0);
}
