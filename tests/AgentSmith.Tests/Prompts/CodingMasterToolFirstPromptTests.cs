using AgentSmith.Infrastructure.Core.Services.Skills;
using FluentAssertions;

namespace AgentSmith.Tests.Prompts;

/// <summary>
/// p0412: run 1b4b ("update all dependencies", two repos) was cancelled at
/// \$10.13 and 165 model calls, still on spec 1 of 3 — the master lifted
/// packages one at a time, building and re-reading between edits, while the
/// toolchain ships a command that establishes the facts and applies the bulk in
/// one pass. Nothing in its instructions told it to look for that command.
///
/// The two rules are authored in agent-smith-skills; this repo carries them only
/// via <c>SkillsCatalogVersion</c>, so the guard is written against the PACKAGED
/// master. 2026-08-28-3302: it used to be gated on the pin and state the gap below
/// <see cref="ToolFirstRelease"/>; the pin has carried these rules since, and a gate
/// whose other arm cannot run is a test that reads as passing without asserting.
/// </summary>
public sealed class CodingMasterToolFirstPromptTests
{
    /// <summary>First agent-smith-skills release carrying the p0412 rules.</summary>
    private static readonly Version ToolFirstRelease = new(4, 4, 0);

    // 2026-11-18-4f3e: coding-agent-master 3.0.0 (v5.36.0) says it in one sentence; verify-once left with the
    // master that edited by itself — the lead builds and tests once per round of a sub-agent's work.
    private const string ToolFirstRule =
        "For a large uniform change, a script or the ecosystem's codemod does the bulk and the compiler's error list enumerates the sites";

    private const string InstallRule = "Install what the project needs the ecosystem's own way";

    [Fact]
    public void PackagedCodingMaster_StatesToolFirst()
    {
        var catalog = new EmbeddedSkillsCatalog();
        var master = PackagedMaster.Folded("coding-agent-master");

        master.Should().Contain(ToolFirstRule,
            $"a master told only to do the work hand-edits what a command already knows; "
            + $"the pin is {catalog.Version} and this wording ships from v{ToolFirstRelease}");
        master.Should().Contain(InstallRule, "a missing tool is installed, not worked around by hand");
    }

    /// <summary>
    /// The tool establishes the facts and does the bulk; the master judges the
    /// exceptions. Dropping that half turns an expensive correct run into a cheap
    /// wrong one — a blanket bulk run is exactly what gets the few sites the
    /// ticket cares about wrong.
    /// </summary>
    [Fact]
    public void PackagedCodingMaster_KeepsJudgmentWithTheMaster()
    {
        PackagedMaster.Folded("coding-agent-master").Should().Contain("the residue is judgment",
            "tool-first without the exception clause is 'tool instead of model'");
    }
}
