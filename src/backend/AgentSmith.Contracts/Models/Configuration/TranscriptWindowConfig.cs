namespace AgentSmith.Contracts.Models.Configuration;

/// <summary>
/// 2026-11-12-cd4cl: the part of a long design conversation a dialog turn's prompt carries, from
/// <c>agent.dialog_transcript</c>. Up to <see cref="VerbatimChars"/> the transcript is rendered whole; past that a fold
/// is due, folding down to <see cref="KeepRecentChars"/> and never the newest <see cref="MinRecentTurns"/>, and one
/// fold reads at most <see cref="MaxFoldChars"/>.
/// </summary>
public sealed class TranscriptWindowConfig
{
    public int VerbatimChars { get; set; } = 60_000;
    public int KeepRecentChars { get; set; } = 30_000;
    public int MinRecentTurns { get; set; } = 2;
    public int MaxFoldChars { get; set; } = 200_000;
}
