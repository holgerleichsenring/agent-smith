namespace AgentSmith.Server.Models;

/// <summary>
/// 2026-09-15-cb3e: one turn of the durable transcript, as the surface renders it. The
/// role is the wire's own word ("user" / "assistant") rather than a number, so a reader of
/// the payload can tell who said what. 2026-09-17-042el: <paramref name="Decision"/> is the
/// wire's word for what an approval answer decided ("approved" / "rejected"), null otherwise.
/// </summary>
/// <para>2026-11-10-d854c: an agent turn names the run it came from, and carries its own proposal when it made one.</para>
public sealed record SpecDialogTurnView(string Role, string Text, DateTimeOffset At, string? Decision)
{
    public string? RunId { get; init; }

    public SpecDialogProposalPush? Proposal { get; init; }
}
