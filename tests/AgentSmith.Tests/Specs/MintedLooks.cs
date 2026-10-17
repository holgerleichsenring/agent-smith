using AgentSmith.Application.Services.Specs;

namespace AgentSmith.Tests.Specs;

/// <summary>2026-10-17-293bc: looks as a derivation minted them, built from their lines — the id is
/// the line's own, the look reached a verdict unless built as one that could not run.</summary>
public static class MintedLooks
{
    public static IReadOnlyList<EvidenceLook> Ran(params string[] lines) =>
        [.. lines.Select(l => Of(l, ran: true))];

    public static EvidenceLook Of(string line, bool ran) =>
        new(DerivationEvidence.IdOf(line)!, "api", line, ran ? 0 : 1, ran, line, EvidenceRecord.Audit);
}
