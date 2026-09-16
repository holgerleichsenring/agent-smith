namespace AgentSmith.Server.Services.SpecDialog;

/// <summary>
/// 2026-09-15-cb3e: the mark-up dialect one spec-dialog CHANNEL reads. Slack and Teams
/// read chat mrkdwn — single-asterisk bold, underscore italic, colon-wrapped emoji
/// shortcodes; a browser reads CommonMark, where the same asterisks mean italic and a
/// shortcode is just text.
/// <para>
/// This is the dialect of the FRAMEWORK's own composed lines and of nothing else. The
/// design master writes ordinary markdown, and its reply travels through the same send
/// method: a converter placed where the two meet cannot tell them apart, so it would turn
/// every italic the model wrote into bold and mangle its fences — corrupting exactly the
/// half of the transcript that carries the thinking. The framework's text is therefore
/// composed for its channel, and the model's is never rewritten.
/// </para>
/// </summary>
public sealed record SpecDialogMarkup
{
    /// <summary>What Slack and Teams render.</summary>
    public static readonly SpecDialogMarkup ChatMrkdwn = new("*", "_", withShortcodes: true);

    /// <summary>What a browser renders.</summary>
    public static readonly SpecDialogMarkup CommonMark = new("**", "*", withShortcodes: false);

    private readonly string _bold;
    private readonly string _italic;
    private readonly bool _withShortcodes;

    private SpecDialogMarkup(string bold, string italic, bool withShortcodes)
    {
        _bold = bold;
        _italic = italic;
        _withShortcodes = withShortcodes;
    }

    /// <summary>The dialect the named platform's channel reads.</summary>
    public static SpecDialogMarkup For(string platform) =>
        string.Equals(platform, DispatcherDefaults.PlatformDashboard, StringComparison.OrdinalIgnoreCase)
            ? CommonMark
            : ChatMrkdwn;

    public string Bold(string text) => $"{_bold}{text}{_bold}";

    public string Italic(string text) => $"{_italic}{text}{_italic}";

    /// <summary>
    /// The emoji as the channel carries it: a shortcode where the chat client expands one,
    /// the glyph itself where nothing would — a browser showing ":question:" says only that
    /// the text was written for somewhere else.
    /// </summary>
    public string Emoji(string shortcode, string glyph) =>
        _withShortcodes ? $":{shortcode}:" : glyph;
}
