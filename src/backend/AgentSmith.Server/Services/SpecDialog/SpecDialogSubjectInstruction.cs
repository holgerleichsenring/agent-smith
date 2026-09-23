namespace AgentSmith.Server.Services.SpecDialog;

/// <summary>
/// 2026-09-23-35b8: what the model is told when it names a conversation. Extracted from
/// <see cref="SpecDialogSubjectMinter"/>, which makes the call — this is prompt content, and
/// the minter's own responsibility does not change when the wording does.
/// <para>
/// It names ONE language: the conversation's. An earlier wording also named English, as the
/// language to avoid, and a prohibition is the only place an instruction can put a language
/// the conversation is not written in.
/// </para>
/// </summary>
internal static class SpecDialogSubjectInstruction
{
    public const string Text =
        "You are naming a design conversation, for a heading above it.\n"
        + "Answer with ONE short line naming what the conversation is ABOUT, and with nothing "
        + "else: no quotation marks, no code fence, no markdown, no list, no closing full stop.\n"
        + "Write it in the SAME LANGUAGE the conversation is written in.\n"
        + "Keep it under 120 characters. Do not restate the question; name the subject.";
}
