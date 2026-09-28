using System.Text;
using AgentSmith.Domain.Models;

namespace AgentSmith.Application.Services.Handlers;

/// <summary>
/// The sections of a run record that say what the run was HELD TO — the contract it
/// accepted, the account it gave of that contract, and the ticket instructions it refused.
/// <para>
/// p0429a gathered them out of the formatter: the account was the third of a kind, and a
/// formatter that grows one method per obligation is how a 257-line file stays 257 lines.
/// </para>
/// </summary>
internal static class RunContractSections
{
    /// <summary>
    /// p0429a: what the run — or the scan — accounted for, itemised. The gate has judged
    /// this since p0421 and no reader has ever seen it: a scan whose dependency audit died
    /// read exactly like one that audited and found nothing.
    /// </summary>
    internal static void AppendAccount(StringBuilder sb, string? account)
    {
        if (string.IsNullOrWhiteSpace(account)) return;
        sb.AppendLine();
        sb.AppendLine(account.Trim());
    }

    /// <summary>2026-09-06-3d81: the criteria the master declined, with the evaluated meaning
    /// of not doing them — beside the account, so the reader of the record sees which
    /// sentences of the ticket no work could satisfy.</summary>
    internal static void AppendDeclined(StringBuilder sb, string? declined)
    {
        if (string.IsNullOrWhiteSpace(declined)) return;
        sb.AppendLine();
        sb.AppendLine(declined.Trim());
    }

    // p0316: surface ticket instructions the master refused (out-of-scope / destructive /
    // injection) as an operator-visible, auditable section — verbatim quote + reason.
    internal static void AppendIgnoredInstructions(
        StringBuilder sb, IReadOnlyList<IgnoredInstruction>? ignored)
    {
        if (ignored is null || ignored.Count == 0) return;
        sb.AppendLine();
        sb.AppendLine("## Ignored ticket instructions");
        sb.AppendLine();
        sb.AppendLine(
            "The following instructions embedded in the ticket were NOT followed "
            + "(out of scope, unsafe, or an attempt to override the agent's rules):");
        sb.AppendLine();
        foreach (var i in ignored)
            sb.AppendLine($"- **\"{i.Quote.Trim()}\"** — {i.Reason.Trim()}");
    }
}
