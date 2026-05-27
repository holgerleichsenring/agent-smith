using AgentSmith.Server.Services;

namespace AgentSmith.Server.Services.Adapters;

/// <summary>
/// Creates individual Slack Block Kit input blocks for the modal UI.
/// </summary>
internal static class SlackModalBlockFactory
{
    internal static readonly (string Value, string Label)[] CommandOptions =
    [
        ("fix_bug", "Fix Bug"), ("fix_bug_no_tests", "Fix Bug (no tests)"),
        ("add_feature", "Add Feature"), ("security_review", "Security Review"),
        ("mad_discussion", "MAD Discussion"), ("legal_analysis", "Legal Analysis"),
        ("list_tickets", "List Tickets"), ("create_ticket", "Create Ticket"),
        ("init_project", "Init Project")
    ];

    public static object BuildCommandBlock(string? selectedValue = null)
    {
        var options = CommandOptions.Select(c => new
        {
            text = new { type = "plain_text", text = c.Label }, value = c.Value
        }).ToArray();

        var initialOption = selectedValue is not null
            ? new { text = new { type = "plain_text", text = CommandOptions.First(c => c.Value == selectedValue).Label }, value = selectedValue }
            : (object?)null;

        return new
        {
            type = "input", block_id = DispatcherDefaults.SlackBlockCommand,
            dispatch_action = true,
            label = new { type = "plain_text", text = "Command" },
            element = initialOption is not null
                ? (object)new { type = "static_select", action_id = DispatcherDefaults.SlackActionCommand, initial_option = initialOption, options }
                : new { type = "static_select", action_id = DispatcherDefaults.SlackActionCommand, placeholder = new { type = "plain_text", text = "Select a command..." }, options }
        };
    }

    public static object BuildProjectBlock(string? selectedProject = null) => new
    {
        type = "input", block_id = DispatcherDefaults.SlackBlockProject,
        dispatch_action = true,
        label = new { type = "plain_text", text = "Project" },
        element = !string.IsNullOrWhiteSpace(selectedProject)
            ? (object)new
            {
                type = "external_select", action_id = DispatcherDefaults.SlackActionProject,
                placeholder = new { type = "plain_text", text = "Search for a project..." },
                min_query_length = 0,
                initial_option = new { text = new { type = "plain_text", text = selectedProject }, value = selectedProject }
            }
            : new
            {
                type = "external_select", action_id = DispatcherDefaults.SlackActionProject,
                placeholder = new { type = "plain_text", text = "Search for a project..." },
                min_query_length = 0
            }
    };

    public static object BuildTicketBlock() => new
    {
        type = "input", block_id = DispatcherDefaults.SlackBlockTicket,
        label = new { type = "plain_text", text = "Ticket" },
        element = new
        {
            type = "external_select", action_id = DispatcherDefaults.SlackActionTicket,
            placeholder = new { type = "plain_text", text = "Search for a ticket..." },
            min_query_length = 1
        }
    };

    public static object BuildTitleBlock() => new
    {
        type = "input", block_id = DispatcherDefaults.SlackBlockTitle,
        label = new { type = "plain_text", text = "Title" },
        element = new
        {
            type = "plain_text_input", action_id = "title_input",
            placeholder = new { type = "plain_text", text = "Enter ticket title..." }
        }
    };

    public static object BuildDescriptionBlock() => new
    {
        type = "input", block_id = DispatcherDefaults.SlackBlockDescription,
        optional = true,
        label = new { type = "plain_text", text = "Description" },
        element = new
        {
            type = "plain_text_input", action_id = "desc_input", multiline = true,
            placeholder = new { type = "plain_text", text = "Enter ticket description (optional)..." }
        }
    };
}
