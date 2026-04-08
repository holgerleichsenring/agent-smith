using System.Text;
using AgentSmith.Application.Services.Handlers;
using AgentSmith.Contracts.Dialogue;
using FluentAssertions;

namespace AgentSmith.Tests.Services;

public sealed class RunResultFormatterDialogueTrailTests
{
    [Fact]
    public void AppendDialogueTrail_WithEntries_RendersTable()
    {
        var sb = new StringBuilder();
        var entries = new List<DialogTrailEntry>
        {
            new(
                new DialogQuestion("q1", QuestionType.Confirmation, "Should I proceed?",
                    "Ambiguous spec", null, "yes", TimeSpan.FromMinutes(5)),
                new DialogAnswer("q1", "Yes", null,
                    new DateTimeOffset(2025, 3, 15, 14, 3, 12, TimeSpan.Zero), "@holger")),
            new(
                new DialogQuestion("q2", QuestionType.Choice, "Which approach?",
                    "Two valid options", new List<string> { "A", "B" }.AsReadOnly(), "A", TimeSpan.FromMinutes(5)),
                new DialogAnswer("q2", "B", null,
                    new DateTimeOffset(2025, 3, 15, 14, 10, 0, TimeSpan.Zero), "@dev"))
        };

        RunResultFormatter.AppendDialogueTrail(sb, entries);
        var result = sb.ToString();

        result.Should().Contain("## Dialogue Trail");
        result.Should().Contain("| Time | Question | Type | Answer | By | Timeout? |");
        result.Should().Contain("Should I proceed?");
        result.Should().Contain("Confirmation");
        result.Should().Contain("@holger");
        result.Should().Contain("No");
        result.Should().Contain("Which approach?");
        result.Should().Contain("Choice");
        result.Should().Contain("@dev");
    }

    [Fact]
    public void AppendDialogueTrail_WithTimeout_ShowsYes()
    {
        var sb = new StringBuilder();
        var entries = new List<DialogTrailEntry>
        {
            new(
                new DialogQuestion("q1", QuestionType.Confirmation, "Continue?",
                    "Test", null, "yes", TimeSpan.FromMinutes(5)),
                new DialogAnswer("q1", "yes", "timeout",
                    DateTimeOffset.UtcNow, "system"))
        };

        RunResultFormatter.AppendDialogueTrail(sb, entries);
        var result = sb.ToString();

        result.Should().Contain("| Yes |");
    }

    [Fact]
    public void AppendDialogueTrail_NullOrEmpty_WritesNothing()
    {
        var sb = new StringBuilder();
        RunResultFormatter.AppendDialogueTrail(sb, null);
        sb.ToString().Should().BeEmpty();

        sb.Clear();
        RunResultFormatter.AppendDialogueTrail(sb, new List<DialogTrailEntry>());
        sb.ToString().Should().BeEmpty();
    }

    [Fact]
    public void AppendDialogueTrail_LongQuestion_Truncates()
    {
        var sb = new StringBuilder();
        var longQuestion = new string('x', 100);
        var entries = new List<DialogTrailEntry>
        {
            new(
                new DialogQuestion("q1", QuestionType.FreeText, longQuestion,
                    "Test", null, "default", TimeSpan.FromMinutes(5)),
                new DialogAnswer("q1", "answer", null, DateTimeOffset.UtcNow, "@user"))
        };

        RunResultFormatter.AppendDialogueTrail(sb, entries);
        var result = sb.ToString();

        result.Should().Contain("...");
        result.Should().NotContain(longQuestion);
    }

    [Fact]
    public void FormatResult_WithDialogueTrail_IncludesSection()
    {
        var ticket = new Domain.Entities.Ticket(
            new Domain.Models.TicketId("1"), "Test", "Desc", null, "Open", "github");
        var plan = new Domain.Entities.Plan("Summary",
            new List<Domain.Entities.PlanStep>
            {
                new(1, "Step 1", new Domain.Models.FilePath("test.cs"), "Create")
            }, "{}");
        var changes = new List<Domain.Entities.CodeChange>
        {
            new(new Domain.Models.FilePath("test.cs"), "content", "Create")
        };

        var dialogueTrail = new List<DialogTrailEntry>
        {
            new(
                new DialogQuestion("q1", QuestionType.Confirmation, "Proceed?",
                    "Test", null, "yes", TimeSpan.FromMinutes(5)),
                new DialogAnswer("q1", "yes", null, DateTimeOffset.UtcNow, "@user"))
        };

        var result = RunResultFormatter.FormatResult(
            ticket, plan, changes, 1, 0, null, null, null, null, dialogueTrail);

        result.Should().Contain("## Dialogue Trail");
        result.Should().Contain("Proceed?");
    }

    [Fact]
    public void FormatResult_WithoutDialogueTrail_OmitsSection()
    {
        var ticket = new Domain.Entities.Ticket(
            new Domain.Models.TicketId("1"), "Test", "Desc", null, "Open", "github");
        var plan = new Domain.Entities.Plan("Summary",
            new List<Domain.Entities.PlanStep>
            {
                new(1, "Step 1", new Domain.Models.FilePath("test.cs"), "Create")
            }, "{}");
        var changes = new List<Domain.Entities.CodeChange>
        {
            new(new Domain.Models.FilePath("test.cs"), "content", "Create")
        };

        var result = RunResultFormatter.FormatResult(
            ticket, plan, changes, 1, 0, null, null);

        result.Should().NotContain("## Dialogue Trail");
    }
}
