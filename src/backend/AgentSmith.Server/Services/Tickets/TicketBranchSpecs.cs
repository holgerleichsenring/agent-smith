using AgentSmith.Application.Services.Specs;
using AgentSmith.Contracts.Specs;
using AgentSmith.Domain.Models;

namespace AgentSmith.Server.Services.Tickets;

/// <summary>
/// 2026-11-17-a0b6b: the spec set a ticket's branch holds, for a reader that knows the ticket by project
/// and id — the one place the server takes a spec's text from. The project gives the repositories and the
/// tracker type the ticket key is spelled with; <see cref="TicketBranchSeries"/> reads the branch.
/// </summary>
public sealed class TicketBranchSpecs(TicketProjects projects, TicketBranchSeries branch)
{
    public async Task<TicketBranchRead> ReadAsync(string projectName, string ticketId, CancellationToken ct)
    {
        if (projects.Of(projectName) is not { } project)
            return TicketBranchRead.Unread($"project '{projectName}' is no longer in the configuration catalog");
        var key = TicketKey.For(project.Tracker.Type.ToString().ToLowerInvariant(), ticketId);
        var read = await branch.ReadAsync(project, key, new TicketId(ticketId), ct);
        return read.State switch
        {
            SpecSetBranchState.Answered => new TicketBranchRead(read.Set, null),
            SpecSetBranchState.NothingAtThePath => TicketBranchRead.None,
            _ => TicketBranchRead.Unread(read.Why ?? "the ticket branch did not read back as a series"),
        };
    }
}

/// <summary>2026-11-17-a0b6b: the branch's set, or none, or why it could not be read.</summary>
public sealed record TicketBranchRead(SpecSet? Set, string? Why)
{
    public static readonly TicketBranchRead None = new(null, null);

    public static TicketBranchRead Unread(string why) => new(null, why);

    /// <summary>The branch's spec of this id; null when the branch holds none.</summary>
    public DerivedSpec? Spec(string specId) =>
        Set?.Specs.FirstOrDefault(s => string.Equals(s.SpecId, specId, StringComparison.Ordinal));
}
