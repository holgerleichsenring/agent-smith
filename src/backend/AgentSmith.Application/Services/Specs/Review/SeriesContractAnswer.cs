namespace AgentSmith.Application.Services.Specs.Review;

/// <summary>2026-10-13-8762k: the series-contract reviewer's answer as it wrote it, before admission.
/// <see cref="Verdict"/> is already one of the <see cref="Contracts.Events.SpecReviewVerdicts"/>.</summary>
internal sealed record SeriesContractAnswer(
    string Verdict, IReadOnlyList<SeriesContractItem> Cuts, IReadOnlyList<SeriesContractItem> Findings);

/// <summary>2026-10-13-8762k: one cut or finding as written — the member it names, the contract entry it quotes
/// (a design turn's evidence), and the attack item it otherwise is.</summary>
internal sealed record SeriesContractItem(string? SpecId, string? Entry, SpecAttackItem Item);
