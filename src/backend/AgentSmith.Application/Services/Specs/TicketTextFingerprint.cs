using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using AgentSmith.Domain.Entities;

namespace AgentSmith.Application.Services.Specs;

/// <summary>
/// 2026-09-08-5cd2: the fingerprint of the ticket text a spec set was cut from — title,
/// description and acceptance criteria, the three fields the derivation prompt renders.
/// Each is read the way the segmenter reads it (HTML to text) with whitespace runs
/// collapsed, so a re-save that changes nothing a reader sees is not an edit. The next
/// run compares it against the ticket it fetched: a difference is input the model has
/// not seen, and the unstarted tail is cut again from the current text.
/// </summary>
public static partial class TicketTextFingerprint
{
    public static string Of(Ticket ticket)
    {
        ArgumentNullException.ThrowIfNull(ticket);
        var text = string.Join(
            "\n",
            Normalize(ticket.Title), Normalize(ticket.Description), Normalize(ticket.AcceptanceCriteria));
        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(text)));
    }

    private static string Normalize(string? field) =>
        string.IsNullOrWhiteSpace(field)
            ? string.Empty
            : Whitespace().Replace(TicketHtmlConverter.ToText(field), " ").Trim();

    [GeneratedRegex(@"\s+")]
    private static partial Regex Whitespace();
}
