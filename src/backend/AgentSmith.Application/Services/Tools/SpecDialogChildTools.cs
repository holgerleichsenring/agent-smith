using Microsoft.Extensions.AI;

namespace AgentSmith.Application.Services.Tools;

/// <summary>
/// The surface a CHILD of a dialog turn runs under: the read tools of its parent's base surface, by name, and
/// nothing else. An allowlist rather than a list of exclusions, so a tool that joins the base surface later — an
/// act on the ticket, a write, a question to the person — reaches no child until it is named a read here.
/// <para>
/// What it leaves out, and why: <c>ask_human</c>, because a session holds one pending question and two children
/// asking would strand each other; <c>remember</c>, because the read-only scope refuses that write by throwing;
/// <c>http_request</c>, because it sends any method; <c>withdraw_filed_ticket</c> and every act on the ticket,
/// because a child acts on no person's word.
/// </para>
/// </summary>
public static class SpecDialogChildTools
{
    /// <summary>The tools a child may read with.</summary>
    public static readonly IReadOnlySet<string> ReadTools = new HashSet<string>(StringComparer.Ordinal)
    {
        "read_file", "grep_in_file", "grep_in_tree", "find_files", "list_directory", "directory_tree",
        "web_fetch", "recall", "read_ticket", "ticket_work", "ticket_runs",
    };

    /// <summary>The read tools of the parent's surface.</summary>
    public static IList<AITool> Of(IList<AITool> specDialogSurface)
    {
        ArgumentNullException.ThrowIfNull(specDialogSurface);
        return [.. specDialogSurface.Where(tool => ReadTools.Contains(tool.Name))];
    }
}
