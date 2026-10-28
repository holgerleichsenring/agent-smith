using AgentSmith.Contracts.Tickets;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Models;
using AgentSmith.Infrastructure.Services.Providers.Tickets;
using FluentAssertions;

namespace AgentSmith.Tests.Providers.Tickets;

/// <summary>
/// 2026-09-25-8e51e: what a ticket rewrite may touch, and what it may not.
/// <para>
/// The region replacement is ONE function and all three implementing trackers call it, so the
/// promise "your own prose survives" is pinned here once rather than three times against three
/// transports that cannot be reached from a test at all. What is per-tracker is pinned per
/// tracker: which Azure DevOps field is written, and Jira's refusal.
/// </para>
/// </summary>
public sealed class TicketRegionRewriteTests
{
    private const string Rendered = "## Goal\nthe widget stops dropping\n";

    /// <summary>
    /// GitHub and GitLab both store the body as the markdown they were given and hand the same
    /// bytes back, so both reach the tracker through this one replacement — the reason those two
    /// can carry a rewrite at all.
    /// </summary>
    [Fact]
    public void Amendment_ProseOutsideTheMarkers_IsUnchangedOnGitHubAndGitLab()
    {
        var body = "A person wrote this above it.\n\n"
            + FramedTicketRegion.Wrap(Rendered)
            + "\nAnd this below it, with a @mention and a [link](https://example.test).";

        var rewritten = FramedTicketRegion.Replace(
            body, FramedTicketRegion.Wrap("## Goal\nthe widget stops dropping, in two specs\n"));

        rewritten.Should().StartWith("A person wrote this above it.")
            .And.EndWith("with a @mention and a [link](https://example.test).");
        rewritten.Should().Contain("in two specs").And.NotContain("stops dropping\n\n<!--",
            "the framework's own rendering is what was replaced");
    }

    /// <summary>
    /// THE COMMON CASE. Every ticket filed before this spec carries a body the framework
    /// rendered whole and no marker saying where its text ends — so nothing is written at all,
    /// and the operator is told why. Replacing the whole body would delete prose nobody can
    /// attribute; appending would leave the ticket saying two things at once.
    /// </summary>
    [Fact]
    public void Amendment_ATicketThatPredatesTheMarkers_IsHandledAsTheSpecStates()
    {
        var filedBefore = "## Goal\nthe widget stops dropping\n\n## Scope\nIn: the widget.\n";

        FramedTicketRegion.Replace(filedBefore, FramedTicketRegion.Wrap(Rendered)).Should().BeNull();
        FramedTicketRegion.Marked(filedBefore).Should().BeFalse();
    }

    /// <summary>2026-10-28-7a69b: a person's ticket takes the region below its text, and an empty one is the region.</summary>
    [Fact]
    public void Region_UnmarkedBodyWithAppend_KeepsTheBodyAndAddsTheRegion()
    {
        var region = FramedTicketRegion.Wrap(Rendered);

        FramedTicketRegion.Replace("Please fix the widget.\n\n", region, RegionPlacement.ReplaceOrAppend)
            .Should().Be($"Please fix the widget.\n\n{region}");
        FramedTicketRegion.Replace(null, region, RegionPlacement.ReplaceOrAppend).Should().Be(region);
    }

    /// <summary>2026-10-28-7a69b: a marker left without its partner is the framework's text somewhere — never a second region.</summary>
    [Fact]
    public void Region_DamagedPairWithAppend_IsRefused() =>
        FramedTicketRegion.Replace($"Please fix it.\n\n{FramedTicketRegion.Begin}\n\nold text", FramedTicketRegion.Wrap(Rendered),
            RegionPlacement.ReplaceOrAppend).Should().BeNull();

    /// <summary>A half-deleted pair is ordinary prose, the way the label note's stripper reads
    /// one: the alternative is a heuristic over text a person edited.</summary>
    [Fact]
    public void Amendment_ABodyWithOneMarkerOfThePair_IsRefusedLikeAnUnmarkedOne() =>
        FramedTicketRegion.Replace(
            $"{FramedTicketRegion.Begin}\n\n{Rendered}", FramedTicketRegion.Wrap(Rendered))
            .Should().BeNull();

    /// <summary>The begin marker's sentence is display text an operator may reword on Jira, so
    /// only the identifier is matched.</summary>
    [Fact]
    public void Amendment_ARewordedBeginMarker_IsStillTheFrameworksRegion()
    {
        var body = $"<!-- {FramedTicketRegion.BeginIdentifier} - reworded by hand -->\n\n"
            + $"old\n\n{FramedTicketRegion.End}\n";

        FramedTicketRegion.Replace(body, FramedTicketRegion.Wrap("new"))
            .Should().NotBeNull().And.Subject.As<string>().Should().Contain("new").And.NotContain("old");
    }

    /// <summary>
    /// p0318 found the read half: a Bug keeps its body in reproduction steps and its description
    /// is empty. Writing the amendment into the description there would give the work item a
    /// SECOND body — and the reader, which prefers the description, would show that one instead.
    /// So one chooser answers for both, and this pins that it is the same answer.
    /// </summary>
    [Fact]
    public void Amendment_OnAzureDevOps_WritesTheFieldTheWorkItemTypeReads()
    {
        var bug = new Dictionary<string, object>
        {
            ["System.Title"] = "Widget drops",
            [AzureDevOpsBodyField.ReproSteps] = "<p>it drops</p>",
        };
        var task = new Dictionary<string, object>
        {
            ["System.Title"] = "Widget drops",
            [AzureDevOpsBodyField.Description] = "<p>it drops</p>",
        };

        AzureDevOpsBodyField.Of(bug).Should().Be(AzureDevOpsBodyField.ReproSteps);
        AzureDevOpsBodyField.Of(task).Should().Be(AzureDevOpsBodyField.Description);
        // The read half, through the mapper the run and the conversation both read tickets with.
        new AzureDevOpsFieldMapper().Map(new TicketId("1"), bug).Description
            .Should().Be(AzureDevOpsBodyField.Read(bug, AzureDevOpsBodyField.Of(bug)));
        new AzureDevOpsFieldMapper().Map(new TicketId("1"), task).Description
            .Should().Be(AzureDevOpsBodyField.Read(task, AzureDevOpsBodyField.Of(task)));
    }

    /// <summary>A work item with no body at all is written where every type keeps one.</summary>
    [Fact]
    public void Amendment_OnAWorkItemWithNoBody_WritesTheDescription() =>
        AzureDevOpsBodyField.Of(new Dictionary<string, object> { ["System.Title"] = "t" })
            .Should().Be(AzureDevOpsBodyField.Description);


    /// <summary>
    /// The filed body IS the region: a filing and an amendment write the same bytes, so a ticket
    /// filed today can be amended tomorrow without anything in between knowing the difference.
    /// </summary>
    [Fact]
    public void Filing_TheBodyItWrites_IsMarkedAsTheFrameworksOwnRegion()
    {
        var body = new AgentSmith.Application.Services.SpecDialog.SpecTicketRenderer()
            .RenderSpec(new Contracts.Models.SpecDraft(
                "p9001", "the widget stops dropping", "spec: p9001", [])).Body;

        FramedTicketRegion.Marked(body).Should().BeTrue();
        body.Should().Contain("the widget stops dropping");
    }

    /// <summary>The label note keeps its own inner pair, which the ticket-fetch door strips; the
    /// outer region says nothing about which parts of itself a model may read.</summary>
    [Fact]
    public void Filing_TheLabelNote_StillSitsInsideItsOwnPair()
    {
        var body = new AgentSmith.Application.Services.SpecDialog.SpecTicketRenderer()
            .RenderSpec(
                new Contracts.Models.SpecDraft("p9001", "goal", "spec: p9001", []), "job-1",
                AgentSmith.Application.Services.SpecDialog.TicketLabelNote.For(
                    [AgentSmith.Application.Services.SpecDialog.FiledTicketLabels.ApprovedSetStamp],
                    AgentSmith.Application.Services.SpecDialog.FiledTicketLabels.ApprovedSetStamp)).Body;

        body.Should().Contain(AgentSmith.Application.Services.SpecDialog.TicketLabelNote.BeginIdentifier);
        AgentSmith.Application.Services.Specs.TicketLabelNoteStripper.Strip(body)
            .Should().NotContain(AgentSmith.Application.Services.SpecDialog.TicketLabelNote.BeginIdentifier)
            .And.Contain(FramedTicketRegion.BeginIdentifier,
                "the outer region survives the stripper — it is not bookkeeping, it is the body");
    }
}
