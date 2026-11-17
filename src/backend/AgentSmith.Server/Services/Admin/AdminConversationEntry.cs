using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Server.Models;

namespace AgentSmith.Server.Services.Admin;

/// <summary>
/// 2026-11-17-f3a8a: the installation's own conversation, offered among the projects a new conversation can be
/// opened on, after them (a default pick stays a project) — to a caller who holds the admin assistant's grant, and to nobody else.
/// </summary>
internal static class AdminConversationEntry
{
    internal static SpecDialogView Offered(SpecDialogView view, bool seesInstallation) =>
        seesInstallation
            ? view with { Projects = [.. view.Projects, new SpecDialogProjectView(InstallationScope.Name, [], [])] }
            : view;
}
