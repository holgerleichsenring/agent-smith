namespace AgentSmith.Domain.Models;

/// <summary>
/// 2026-08-30-0ea8: one entry of the published verification standard the product
/// ingested, kept exactly as the standard states it.
/// <para>
/// <paramref name="Id"/> is the standard's own requirement id — the citation an answer
/// carries, which is only meaningful together with the catalogue version that issued it.
/// <paramref name="Level"/> is the standard's level value as the export writes it (the
/// strings "1", "2", "3"), not a severity of ours. <paramref name="Text"/> is verbatim:
/// nothing here is paraphrased, renumbered or re-scored, because a citation of an edited
/// clause cites nothing.
/// </para>
/// </summary>
public sealed record VerificationRequirement(string Id, string Level, string Text);
