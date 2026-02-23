using AgentSmith.Dispatcher.Models;
using AgentSmith.Dispatcher.Services;

namespace AgentSmith.Dispatcher.Services.Adapters;

/// <summary>
/// Builds Slack Block Kit modal views for the structured command UI.
/// Follows the SlackErrorBlockBuilder pattern: static class, anonymous object trees.
/// </summary>
internal static class SlackModalBuilder
{
    private static readonly (string Value, string Label)[] CommandOptions =
    [
        ("fix_ticket", "Fix Ticket"),
        ("list_tickets", "List Tickets"),
        ("create_ticket", "Create Ticket"),
        ("init_project", "Init Project")
    ];

    /// <summary>
    /// Builds the initial modal view with command dropdown and project external_select.
    /// </summary>
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
            BuildCommandBlock(),
            BuildProjectBlock()
        }
    };

    /// <summary>
    /// Builds an updated modal view with conditional fields based on selected command.
    /// </summary>
    public static object BuildUpdatedView(
        ModalCommandType command,
        string privateMetadata,
        string? selectedProject,
        IReadOnlyList<string>? pipelineNames = null)
    {
        var blocks = new List<object>
        {
            BuildCommandBlockWithSelection(command),
            BuildProjectBlockWithSelection(selectedProject)
        };

        switch (command)
        {
            case ModalCommandType.FixTicket:
                blocks.Add(BuildTicketBlock());
                if (pipelineNames is { Count: > 1 })
                    blocks.Add(BuildPipelineBlock(pipelineNames));
                break;

            case ModalCommandType.CreateTicket:
                blocks.Add(BuildTitleBlock());
                blocks.Add(BuildDescriptionBlock());
                break;

            // ListTickets and InitProject need no additional fields
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

    /// <summary>
    /// Builds the options response for the project external_select dropdown.
    /// </summary>
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

    /// <summary>
    /// Builds the options response for the ticket external_select dropdown.
    /// </summary>
    public static object BuildTicketOptions(IReadOnlyList<(int Id, string Title)> tickets, string? searchQuery)
    {
        var filtered = string.IsNullOrWhiteSpace(searchQuery)
            ? tickets
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

    /// <summary>
    /// Parses a command value string to ModalCommandType.
    /// </summary>
    public static ModalCommandType? ParseCommandValue(string? value) => value switch
    {
        "fix_ticket" => ModalCommandType.FixTicket,
        "list_tickets" => ModalCommandType.ListTickets,
        "create_ticket" => ModalCommandType.CreateTicket,
        "init_project" => ModalCommandType.InitProject,
        _ => null
    };

    // --- Block builders ---

    private static object BuildCommandBlock() => new
    {
        type = "input",
        block_id = DispatcherDefaults.SlackBlockCommand,
        dispatch_action = true,
        label = new { type = "plain_text", text = "Command" },
        element = new
        {
            type = "static_select",
            action_id = DispatcherDefaults.SlackActionCommand,
            placeholder = new { type = "plain_text", text = "Select a command..." },
            options = CommandOptions.Select(c => new
            {
                text = new { type = "plain_text", text = c.Label },
                value = c.Value
            }).ToArray()
        }
    };

    private static object BuildCommandBlockWithSelection(ModalCommandType command)
    {
        var selectedValue = command switch
        {
            ModalCommandType.FixTicket => "fix_ticket",
            ModalCommandType.ListTickets => "list_tickets",
            ModalCommandType.CreateTicket => "create_ticket",
            ModalCommandType.InitProject => "init_project",
            _ => "fix_ticket"
        };

        return new
        {
            type = "input",
            block_id = DispatcherDefaults.SlackBlockCommand,
            dispatch_action = true,
            label = new { type = "plain_text", text = "Command" },
            element = new
            {
                type = "static_select",
                action_id = DispatcherDefaults.SlackActionCommand,
                initial_option = new
                {
                    text = new { type = "plain_text", text = CommandOptions.First(c => c.Value == selectedValue).Label },
                    value = selectedValue
                },
                options = CommandOptions.Select(c => new
                {
                    text = new { type = "plain_text", text = c.Label },
                    value = c.Value
                }).ToArray()
            }
        };
    }

    private static object BuildProjectBlock() => new
    {
        type = "input",
        block_id = DispatcherDefaults.SlackBlockProject,
        dispatch_action = true,
        label = new { type = "plain_text", text = "Project" },
        element = new
        {
            type = "external_select",
            action_id = DispatcherDefaults.SlackActionProject,
            placeholder = new { type = "plain_text", text = "Search for a project..." },
            min_query_length = 0
        }
    };

    private static object BuildProjectBlockWithSelection(string? selectedProject)
    {
        if (string.IsNullOrWhiteSpace(selectedProject))
            return BuildProjectBlock();

        return new
        {
            type = "input",
            block_id = DispatcherDefaults.SlackBlockProject,
            dispatch_action = true,
            label = new { type = "plain_text", text = "Project" },
            element = new
            {
                type = "external_select",
                action_id = DispatcherDefaults.SlackActionProject,
                placeholder = new { type = "plain_text", text = "Search for a project..." },
                min_query_length = 0,
                initial_option = new
                {
                    text = new { type = "plain_text", text = selectedProject },
                    value = selectedProject
                }
            }
        };
    }

    private static object BuildTicketBlock() => new
    {
        type = "input",
        block_id = DispatcherDefaults.SlackBlockTicket,
        label = new { type = "plain_text", text = "Ticket" },
        element = new
        {
            type = "external_select",
            action_id = DispatcherDefaults.SlackActionTicket,
            placeholder = new { type = "plain_text", text = "Search for a ticket..." },
            min_query_length = 1
        }
    };

    private static object BuildTitleBlock() => new
    {
        type = "input",
        block_id = DispatcherDefaults.SlackBlockTitle,
        label = new { type = "plain_text", text = "Title" },
        element = new
        {
            type = "plain_text_input",
            action_id = "title_input",
            placeholder = new { type = "plain_text", text = "Enter ticket title..." }
        }
    };

    private static object BuildDescriptionBlock() => new
    {
        type = "input",
        block_id = DispatcherDefaults.SlackBlockDescription,
        optional = true,
        label = new { type = "plain_text", text = "Description" },
        element = new
        {
            type = "plain_text_input",
            action_id = "desc_input",
            multiline = true,
            placeholder = new { type = "plain_text", text = "Enter ticket description (optional)..." }
        }
    };

    private static object BuildPipelineBlock(IReadOnlyList<string> pipelineNames) => new
    {
        type = "input",
        block_id = DispatcherDefaults.SlackBlockPipeline,
        optional = true,
        label = new { type = "plain_text", text = "Pipeline" },
        element = new
        {
            type = "static_select",
            action_id = DispatcherDefaults.SlackActionPipeline,
            placeholder = new { type = "plain_text", text = "Override pipeline (optional)..." },
            options = pipelineNames.Select(p => new
            {
                text = new { type = "plain_text", text = p },
                value = p
            }).ToArray()
        }
    };

    private static string TruncateOptionText(string text) =>
        text.Length <= 75 ? text : text[..72] + "...";
}
