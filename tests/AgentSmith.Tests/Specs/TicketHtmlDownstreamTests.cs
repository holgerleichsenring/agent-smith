using AgentSmith.Application.Models;
using AgentSmith.Application.Services.Expectations;
using AgentSmith.Application.Services.Handlers;
using AgentSmith.Application.Services.Scope;
using AgentSmith.Application.Services.Specs;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Models;
using AgentSmith.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentSmith.Tests.Specs;

/// <summary>
/// 2026-09-24-b3c1: an Azure DevOps body arrives as HTML. The derivation path has read it as
/// TEXT since p0399; these readers were never joined to the same converter and carried the
/// transport encoding instead — the coding master, the scope classifier, the expectation
/// composer, the cut review, the pull-request body and the event the run viewer renders.
/// Sibling of TicketLabelNoteDownstreamTests, which holds the same readers against the note.
/// </summary>
public sealed class TicketHtmlDownstreamTests
{
    // Exactly the shape Azure DevOps returns for a ticket written in its own editor.
    private const string Html =
        "<h2 id=goal>Goal </h2><p>Update all direct dependencies. </p>"
        + "<h2 id=scope>Scope </h2><ul><li>In: declared NuGet dependencies &amp; npm</li></ul>";

    private static Ticket HtmlTicket() =>
        new(new TicketId("42"), "Dependency update", Html, "<ul><li>the build is green</li></ul>",
            "New", "AzureDevOps");

    private static Ticket TextTicket() =>
        new(new TicketId("42"), "Dependency update", "## Goal\nUpdate all direct dependencies.",
            "- the build is green", "New", "AzureDevOps");

    [Fact]
    public void Build_ADescriptionInHtml_ReachesTheCodingMasterAsText()
    {
        var prompt = MasterUserPrompt.Build(
            HtmlTicket(), new Repository(new BranchName("run/42"), "https://git.test/app"),
            [], string.Empty, string.Empty);

        prompt.Should().Contain("Update all direct dependencies")
            .And.Contain("the build is green")
            .And.Contain("declared NuGet dependencies & npm", "the entity is decoded, not carried")
            .And.NotContain("<h2").And.NotContain("<li>").And.NotContain("&amp;");
    }

    [Fact]
    public void Build_ADescriptionInPlainText_IsUnchanged()
    {
        var prompt = MasterUserPrompt.Build(
            TextTicket(), new Repository(new BranchName("run/42"), "https://git.test/app"),
            [], string.Empty, string.Empty);

        prompt.Should().Contain("## Goal\nUpdate all direct dependencies.")
            .And.Contain("- the build is green");
    }

    [Fact]
    public void Build_ATicketWithNoCriteria_StillSaysNoneSpecified()
    {
        var ticket = new Ticket(
            new TicketId("42"), "T", "<p>Body</p>", null, "New", "AzureDevOps");

        MasterUserPrompt.Build(
                ticket, new Repository(new BranchName("run/42"), "https://git.test/app"),
                [], string.Empty, string.Empty)
            .Should().Contain("None specified");
    }

    [Fact]
    public async Task Classify_ADescriptionInHtml_ReachesTheClassifierAsText()
    {
        var chat = new StubChatClient(new Queue<string>(["""{"repos": []}"""]));
        var classifier = new RepoScopeClassifier(
            new StubChatClientFactory(chat), EventTestStubs.RunContext,
            NullLogger<RepoScopeClassifier>.Instance);

        await classifier.ClassifyAsync(
            HtmlTicket(), comments: null, [new RepoConnection { Name = "app" }],
            new Dictionary<string, IReadOnlyList<RemoteContextDiscovery>>(StringComparer.Ordinal),
            new AgentConfig(), new PipelineContext(), CancellationToken.None);

        var prompt = string.Join("\n", chat.LastMessages.Select(m => m.Text));

        prompt.Should().Contain("Update all direct dependencies")
            .And.Contain("the build is green")
            .And.NotContain("<h2").And.NotContain("<li>");
    }

    [Fact]
    public void Compose_ADescriptionInHtml_ReachesTheExpectationPromptAsText()
    {
        var prompt = ExpectationPromptComposer.ComposeUserPrompt(HtmlTicket(), new PipelineContext());

        prompt.Should().Contain("Update all direct dependencies")
            .And.Contain("the build is green")
            .And.NotContain("<h2").And.NotContain("<li>");
    }

    [Fact]
    public void Of_ATicketWhoseBodyIsHtml_IsShownToTheCutReviewAsText()
    {
        var text = SpecCutReviewTicketText.Of(HtmlTicket());

        text.Should().Contain("Dependency update")
            .And.Contain("Update all direct dependencies")
            .And.Contain("the build is green")
            .And.NotContain("<h2").And.NotContain("<li>");
    }

    [Fact]
    public void Of_ATicketWhoseBodyIsPlainText_IsUnchanged()
    {
        SpecCutReviewTicketText.Of(TextTicket()).Should()
            .Be("Dependency update\n\n## Goal\nUpdate all direct dependencies.\n\n- the build is green");
    }
}
