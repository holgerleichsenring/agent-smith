using AgentSmith.Application.Models;
using AgentSmith.Application.Services.Handlers;
using FluentAssertions;

namespace AgentSmith.Tests.Handlers;

/// <summary>
/// 2026-10-04-2bf2: every mode a non-skill round can end in has a sentence; an unknown one
/// throws, so a new mode without one fails here rather than in a live round.
/// </summary>
public sealed class BootstrapPrinciplesOutcomeTests
{
    [Fact]
    public void BootstrapPrinciplesOutcome_Refreshed_HasASentence()
    {
        var sentence = BootstrapPrinciplesOutcome.Sentence(
            new PrinciplesTransferResult(PrinciplesMode.Refreshed, Overlays: ["spark"], ProjectSpecificsKept: true),
            "Project Bootstrap");

        sentence.Should().Contain("refreshed from the authored core+delta+spark")
            .And.Contain("Project Specifics carried over");
    }

    [Fact]
    public void BootstrapPrinciplesOutcome_RefreshedWithoutSpecifics_SaysThereWasNoneToCarry()
    {
        var sentence = BootstrapPrinciplesOutcome.Sentence(
            new PrinciplesTransferResult(PrinciplesMode.Refreshed), "Project Bootstrap");

        sentence.Should().Contain("no Project Specifics section to carry over");
    }
}
