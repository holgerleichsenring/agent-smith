namespace AgentSmith.Application.Services.Browser;

/// <summary>
/// 2026-10-01-283de: the computed-style properties a render reports per selector, and the
/// selectors it reads when the model names none. Stated here once and handed to the browser in
/// the request, so the script carries no list of its own that could drift from this one.
/// </summary>
public static class BrowserStyleProperties
{
    public const int MaxSelectors = 20;
    public const int MaxSelectorLength = 200;

    public static IReadOnlyList<string> Properties { get; } =
    [
        "color", "background-color", "font-family", "font-size", "font-weight", "line-height",
        "letter-spacing", "padding", "margin", "border-radius", "border", "box-shadow", "width", "height",
    ];

    public static IReadOnlyList<string> DefaultSelectors { get; } =
        ["body", "h1", "h2", "h3", "a", "button", "input", "nav", "header", "footer"];
}
