namespace AgentSmith.Application.Services.SpecDialog;

/// <summary>
/// 2026-11-09-0620n: one rule for every re-draft inside a design turn — a schema fix, a review cut. The prose the
/// operator reads is the turn's first full reply, its analysis and questions included, whether or not its block
/// validated; a later reply contributes only its block. Live, a schema fix's one sentence and then a review's one-line
/// note each replaced an analysis with four open questions.
/// </summary>
public static class TurnReply
{
    /// <summary>The first reply with its first draft block replaced by <paramref name="block"/>, or the block appended
    /// when the first reply had none.</summary>
    public static string WithBlock(string firstReply, string block)
    {
        ArgumentNullException.ThrowIfNull(firstReply);
        ArgumentNullException.ThrowIfNull(block);
        return SpecDialogDraftBlocks.Contains(firstReply)
            ? SpecDialogDraftBlocks.Replace(firstReply, block)
            : $"{firstReply.TrimEnd()}\n\n{block}".TrimStart();
    }

    /// <summary>The first reply's prose with the later reply's block; the later reply as it is when it carries none.</summary>
    public static string KeepFirstProse(string firstReply, string laterReply) =>
        SpecDialogDraftBlocks.FirstBlock(laterReply) is { } block ? WithBlock(firstReply, block) : laterReply;
}
