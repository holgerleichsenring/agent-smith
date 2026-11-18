using System.Text;

namespace AgentSmith.Application.Services.Loop;

/// <summary>
/// 2026-11-18-c8e2a: what a master is told when one of its children finishes — who, how it ended, and its whole
/// answer — and, when the child ended before reading what its master wrote, that send_message continues it with it.
/// </summary>
internal static class SubAgentNotice
{
    internal static string Of(SubAgentMember member, SubAgentResult result)
    {
        var text = new StringBuilder()
            .Append(member.Name).Append(" (").Append(member.Id).Append(") ended: ").Append(result.Status);
        if (!string.IsNullOrWhiteSpace(result.FailureReason)) text.Append(" — ").Append(result.FailureReason);
        if (member.Inbox.Count > 0)
            text.Append(". It ended before reading ").Append(member.Inbox.Count)
                .Append(" message(s) you sent; send_message continues it with them");
        if (!string.IsNullOrWhiteSpace(result.Answer)) text.Append(".\n").Append(result.Answer);
        return text.ToString();
    }
}
