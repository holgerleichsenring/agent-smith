using System.Globalization;

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
    public static readonly SpecDialogMarkup ChatMrkdwn =
        new("*", "_", "<{1}|{0}>", withShortcodes: true, onAPage: false);

    /// <summary>What a browser renders.</summary>
    public static readonly SpecDialogMarkup CommonMark =
        new("**", "*", "[{0}]({1})", withShortcodes: false, onAPage: true);

    private readonly string _bold;
    private readonly string _italic;
    private readonly string _link;
    private readonly bool _withShortcodes;
    private readonly bool _onAPage;

    private SpecDialogMarkup(
        string bold, string italic, string link, bool withShortcodes, bool onAPage)
    {
        _bold = bold;
        _italic = italic;
        _link = link;
        _withShortcodes = withShortcodes;
        _onAPage = onAPage;
    }

    /// <summary>The dialect the named platform's channel reads.</summary>
    public static SpecDialogMarkup For(string platform) =>
        string.Equals(platform, DispatcherDefaults.PlatformDashboard, StringComparison.OrdinalIgnoreCase)
            ? CommonMark
            : ChatMrkdwn;

    public string Bold(string text) => $"{_bold}{text}{_bold}";

    public string Italic(string text) => $"{_italic}{text}{_italic}";

    /// <summary>
    /// 2026-09-17-042em: text carrying a url, in the shape the channel links with — CommonMark's
    /// "[text](url)" for a browser, chat mrkdwn's "&lt;url|text&gt;" for Slack. A MARK-UP member,
    /// beside <see cref="Bold"/> and <see cref="Italic"/>, and deliberately not built out of
    /// <see cref="Wording"/>: that one selects what the reader can DO, and a link shape is what
    /// the reader RENDERS. Stated risk, pre-existing and not narrowed here: Teams reads this same
    /// dialect (<see cref="For"/> hands ChatMrkdwn to everything but the dashboard) and renders
    /// markdown rather than Slack's shapes, as it already does for bold and shortcodes.
    /// </summary>
    public string Link(string text, string url) =>
        string.Format(CultureInfo.InvariantCulture, _link, text, url);

    /// <summary>
    /// The emoji as the channel carries it: a shortcode where the chat client expands one,
    /// the glyph itself where nothing would — a browser showing ":question:" says only that
    /// the text was written for somewhere else.
    /// </summary>
    public string Emoji(string shortcode, string glyph) =>
        _withShortcodes ? $":{shortcode}:" : glyph;

    /// <summary>
    /// 2026-09-17-042ek: the wording whose reader can act on it. A chat reader has a thread
    /// to reply in and a slash command to type; the dialog PAGE has neither — it has a box, a
    /// button per decision and a conversation list — so a framework line telling that reader
    /// to reply in a thread or to type "/spec new" names two things the page does not have.
    /// <para>
    /// The dialect decides it because it already is the channel: <see cref="For"/> hands out
    /// <see cref="CommonMark"/> for the dashboard and for nothing else, so one selector
    /// carries both what the reader renders and what the reader can do. It is picked where the
    /// line is BOUND, which is the only place that knows the channel — a
    /// <see cref="ComposedReply"/> is written once and read by both.
    /// </para>
    /// </summary>
    public string Wording(string inChat, string onThePage) => _onAPage ? onThePage : inChat;
}
