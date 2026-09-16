namespace AgentSmith.Server.Services.SpecDialog;

/// <summary>
/// 2026-09-15-cb3e: a line the FRAMEWORK composed, not yet bound to a channel. It becomes
/// text only once a <see cref="SpecDialogMarkup"/> says which dialect the reader speaks.
/// <para>
/// It is a type rather than a convention so the two kinds of text cannot be confused: a
/// composed line reaches <see cref="SpecDialogMessenger"/> through the overload that binds
/// it, and the design master's own reply — a plain string — through the one that sends it
/// unchanged. Neither can take the other's path by accident.
/// </para>
/// </summary>
public sealed record ComposedReply(Func<SpecDialogMarkup, string> Compose)
{
    /// <summary>The line as the given channel reads it.</summary>
    public string In(SpecDialogMarkup markup) => Compose(markup);
}
