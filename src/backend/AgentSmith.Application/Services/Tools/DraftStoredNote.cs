using AgentSmith.Application.Services.SpecDialog.Writes;
using AgentSmith.Contracts.Models;

namespace AgentSmith.Application.Services.Tools;

/// <summary>
/// 2026-11-15-06fc: the one line a write answer adds when the stored file reads otherwise than what was written —
/// a spec whose repositories the checks decided. Stating a spec's own name is no change worth a line, and
/// 2026-11-15-67f1: a ticket, bug or contract is stored as written. 2026-11-17-5701a: a ticket's spec as a spec.
/// </summary>
internal static class DraftStoredNote
{
    public static string Of(string bare, string written, string? stored, ProposalDraft after)
    {
        if (stored is null || Same(stored, written)) return string.Empty;
        if (DraftPath.Item(bare) is not { Kind: DraftItemKind.Spec or DraftItemKind.Epic or DraftItemKind.TicketSpec } item)
            return string.Empty;
        if (Same(stored, $"spec: {item.Name}\n{written.TrimStart()}")) return string.Empty;
        var repos = after.Specs.Select(s => s.Draft).Concat(after.Tickets.SelectMany(t => t.Specs))
            .FirstOrDefault(s => s.SpecId == item.Name)?.Repos ?? [];
        return $"\nThe checks set its repos to {string.Join(", ", repos)}; read_file shows the stored text.";
    }

    private static bool Same(string a, string b) => a.Trim() == b.Trim();
}
