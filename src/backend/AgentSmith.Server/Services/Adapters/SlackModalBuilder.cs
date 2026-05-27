using AgentSmith.Server.Models;
using AgentSmith.Server.Services;

namespace AgentSmith.Server.Services.Adapters;

/// <summary>Builds Slack Block Kit modal views for the structured command UI.</summary>
internal static class SlackModalBuilder
{
    public static object BuildInitialView(string privateMetadata) => new
    {
        type = "modal",
        callback_id = DispatcherDefaults.SlackModalCallbackId,
        private_metadata = privateMetadata,
        title = new { type = "plain_text", text = "Agent Smith" },
        submit = new { type = "plain_text", text = "Execute" },
        close = new { type = "plain_text", text = "Cancel" },
        blocks = new object[]
        {
            SlackModalBlockFactory.BuildCommandBlock(),
            SlackModalBlockFactory.BuildProjectBlock()
        }
    };

    public static object BuildUpdatedView(
        ModalCommandType command, string privateMetadata, string? selectedProject)
    {
        var blocks = new List<object>
        {
            SlackModalBlockFactory.BuildCommandBlock(CommandToValue(command)),
            SlackModalBlockFactory.BuildProjectBlock(selectedProject)
        };

        switch (command)
        {
            case ModalCommandType.FixBug:
            case ModalCommandType.FixBugNoTests:
            case ModalCommandType.AddFeature:
            case ModalCommandType.MadDiscussion:
                blocks.Add(SlackModalBlockFactory.BuildTicketBlock());
                break;
            case ModalCommandType.CreateTicket:
                blocks.Add(SlackModalBlockFactory.BuildTitleBlock());
                blocks.Add(SlackModalBlockFactory.BuildDescriptionBlock());
                break;
        }

        return new
        {
            type = "modal",
            callback_id = DispatcherDefaults.SlackModalCallbackId,
            private_metadata = privateMetadata,
            title = new { type = "plain_text", text = "Agent Smith" },
            submit = new { type = "plain_text", text = "Execute" },
            close = new { type = "plain_text", text = "Cancel" },
            blocks = blocks.ToArray()
        };
    }

    public static object BuildProjectOptions(IReadOnlyList<string> projects, string? searchQuery)
    {
        var filtered = string.IsNullOrWhiteSpace(searchQuery)
            ? projects
            : projects.Where(p => p.Contains(searchQuery, StringComparison.OrdinalIgnoreCase)).ToList();
        return new
        {
            options = filtered.Select(p => new
            {
                text = new { type = "plain_text", text = p },
                value = p
            }).ToArray()
        };
    }

    public static object BuildTicketOptions(IReadOnlyList<(int Id, string Title)> tickets, string? searchQuery)
    {
        var filtered = string.IsNullOrWhiteSpace(searchQuery) ? tickets
            : tickets.Where(t =>
                t.Title.Contains(searchQuery, StringComparison.OrdinalIgnoreCase) ||
                t.Id.ToString().Contains(searchQuery, StringComparison.Ordinal)).ToList();
        return new
        {
            options = filtered.Select(t => new
            {
                text = new { type = "plain_text", text = TruncateOptionText($"#{t.Id} — {t.Title}") },
                value = t.Id.ToString()
            }).ToArray()
        };
    }

    public static ModalCommandType? ParseCommandValue(string? value) => value switch
    {
        "fix_bug" => ModalCommandType.FixBug,
        "fix_bug_no_tests" => ModalCommandType.FixBugNoTests,
        "add_feature" => ModalCommandType.AddFeature,
        "security_review" => ModalCommandType.SecurityReview,
        "mad_discussion" => ModalCommandType.MadDiscussion,
        "legal_analysis" => ModalCommandType.LegalAnalysis,
        "list_tickets" => ModalCommandType.ListTickets,
        "create_ticket" => ModalCommandType.CreateTicket,
        "init_project" => ModalCommandType.InitProject,
        _ => null
    };

    private static string CommandToValue(ModalCommandType command) => command switch
    {
        ModalCommandType.FixBug => "fix_bug",
        ModalCommandType.FixBugNoTests => "fix_bug_no_tests",
        ModalCommandType.AddFeature => "add_feature",
        ModalCommandType.SecurityReview => "security_review",
        ModalCommandType.MadDiscussion => "mad_discussion",
        ModalCommandType.LegalAnalysis => "legal_analysis",
        ModalCommandType.ListTickets => "list_tickets",
        ModalCommandType.CreateTicket => "create_ticket",
        ModalCommandType.InitProject => "init_project",
        _ => "fix_bug"
    };

    private static string TruncateOptionText(string text) =>
        text.Length <= 75 ? text : text[..72] + "...";
}
