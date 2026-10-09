namespace AgentSmith.Server.Services.SpecDialog;

/// <summary>
/// 2026-09-23-35b8: what the model is told when it names a conversation. Extracted from
/// <see cref="SpecDialogSubjectMinter"/>, which makes the call — this is prompt content, and
/// the minter's own responsibility does not change when the wording does.
/// <para>
/// 2026-10-09-753b: it is shown the design partner's first ANSWER and nothing else. The main
/// model chose that answer's language for the person it is talking to, so the answer is the
/// language authority; the person's own message — often mixed, quoting code and naming
/// identifiers — left a small model guessing, and it guessed Spanish twice.
/// </para>
/// </summary>
internal static class SpecDialogSubjectInstruction
{
    public const string Text =
        "You are naming a design conversation, for a heading above it.\n"
        + "You are given the design partner's first answer in it. Answer with ONE short line "
        + "naming what that answer is ABOUT, and with nothing else: no quotation marks, no code "
        + "fence, no markdown, no list, no closing full stop.\n"
        + "Write the line in the language the answer is written in.\n"
        + "Keep it under 120 characters.";

    /// <summary>The one text the heading is condensed from.</summary>
    public static string Exchange(string answer) => $"The answer:\n{answer}";
}
