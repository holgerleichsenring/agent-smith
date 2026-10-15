using System.Security.Claims;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Infrastructure.Persistence.Repositories;
using AgentSmith.Server.Services;
using AgentSmith.Server.Services.SpecDialog;

namespace AgentSmith.Server.Security;

/// <summary>
/// 2026-10-15-4a4db: the project check of the dialog routes that carry their conversation in the BODY,
/// where no middleware reads it. A message's projects are the conversation open on its thread, the
/// project the page names, and the one a typed "/spec &lt;project&gt;" opens on; a bare "/spec" would list
/// every configured project, so only a global holder may type it. Approving starts runs, so whether the
/// turn may is runs.control in those same projects — global when it names none.
/// </summary>
internal sealed class DialogMessageScope(ProjectAccess access, ResourceProjectReader reader, SpecCommandParser parser)
{
    private static readonly string[] Write = [Permissions.DialogWrite];

    public async Task<DialogAdmission> AdmitAsync(
        ClaimsPrincipal user, string dialogId, string? project, string text, CancellationToken ct)
    {
        if (!access.Enforcing) return new DialogAdmission(true, true);
        var typed = parser.Parse(text) is SpecOpenCommand open ? open.Project : string.Empty;
        var projects = await ProjectsAsync(dialogId, [project, typed], ct);
        if (!access.HoldsEverywhere(user, Write)
            && (typed is null || projects.Any(name => !access.HoldsIn(user, Write, name))))
            return DialogAdmission.Refused;
        return new DialogAdmission(true, MayStartRuns(user, projects));
    }

    /// <summary>Whether a turn in these projects may start runs; a non-enforcing installation holds everyone to true.</summary>
    public bool MayStartRuns(ClaimsPrincipal user, IReadOnlyList<string> projects)
    {
        string[] control = [Permissions.RunsControl];
        if (!access.Enforcing) return true;
        return projects.Count == 0
            ? access.HoldsEverywhere(user, control)
            : projects.All(name => access.HoldsIn(user, control, name));
    }

    /// <summary>Whether the conversation open on this thread may be entered — the resume route's target.</summary>
    public async Task<bool> MayEnterAsync(ClaimsPrincipal user, string dialogId, CancellationToken ct)
    {
        if (!access.Enforcing || access.HoldsEverywhere(user, Write)) return true;
        var projects = await ProjectsAsync(dialogId, [], ct);
        return projects.All(name => access.HoldsIn(user, Write, name));
    }

    private async Task<IReadOnlyList<string>> ProjectsAsync(string dialogId, string?[] named, CancellationToken ct) =>
        [.. named.Prepend(await reader.OpenDialogAsync(DispatcherDefaults.PlatformDashboard, dialogId, ct))
            .Where(name => !string.IsNullOrWhiteSpace(name)).Select(name => name!).Distinct(ConfigNames.Comparer)];
}
