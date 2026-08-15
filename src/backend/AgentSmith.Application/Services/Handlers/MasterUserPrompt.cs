using AgentSmith.Application.Services.Prompts;
using AgentSmith.Domain.Entities;

namespace AgentSmith.Application.Services.Handlers;

/// <summary>
/// p0406: renders the coding master's USER prompt — the requirement record (ticket,
/// conversation, attachments) plus the checkouts it may write to. Lifted out of
/// AgenticMasterHandler, which orchestrates the loop and has no business also being
/// a prompt renderer. Sibling of MasterPromptSections / TicketConversationPromptSection.
/// </summary>
internal static class MasterUserPrompt
{
    internal static string Build(
        Ticket? ticket, Repository repo, IEnumerable<string> sandboxKeys,
        string conversationSection, string attachmentsSection)
    {
        var ticketBlock = ticket is null
            ? "(No ticket attached — investigate the repository and proceed per pipeline goal.)"
            // p0316: ticket fields are untrusted — delimit them so an embedded injection
            // ("ignore previous instructions") reads as data, not a command to the master.
            : TicketPromptDelimiters.Wrap($"""
                **ID:** {ticket.Id}
                **Title:** {ticket.Title}
                **Description:** {ticket.Description}
                **Acceptance Criteria:** {ticket.AcceptanceCriteria ?? "None specified"}
                """);

        // p0317: conversation + attachments follow the ticket block — all of it is
        // the requirement record; comment text sits inside the same delimiters.
        var header = string.Join("\n\n",
            new[] { ticketBlock, conversationSection, attachmentsSection }
                .Where(s => !string.IsNullOrEmpty(s)));

        return $"""
            {header}

            ## Working Repositories
            One checkout per repository, all on the run branch:
            {RenderCheckouts(repo, sandboxKeys)}

            Investigate the repositories, plan your change, implement it, and verify
            it (build + tests). Use the available tools — read_file, grep_in_tree,
            edit, write_file, run_command, log_decision, ask_human. When you are
            done, stop calling tools and summarise what changed.
            """;
    }

    // p0384: EVERY checked-out repo is listed (its own sandbox at the checkout path,
    // addressed by repo-prefixed tool paths), not a singular "Working Repository" that
    // silently promoted the first sandbox. Single-repo runs render a one-entry list
    // through the same path.
    private static string RenderCheckouts(Repository repo, IEnumerable<string> sandboxKeys)
    {
        var names = sandboxKeys.Where(k => !string.IsNullOrEmpty(k)).ToList();
        if (names.Count == 0)
            return $"- **Path:** {repo.LocalPath} — **Branch:** {repo.CurrentBranch}";

        var checkouts = string.Join("\n", names.Select(n =>
            $"- `{n}` — checked out at {repo.LocalPath} in its own sandbox — **Branch:** {repo.CurrentBranch}"));
        return names.Count > 1
            ? checkouts
              + $"\nAddress files with the repository prefix (e.g. `{names[0]}/src/...`) so each"
              + " change lands in the right checkout."
            : checkouts;
    }
}
