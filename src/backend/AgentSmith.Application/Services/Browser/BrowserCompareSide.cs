namespace AgentSmith.Application.Services.Browser;

/// <summary>2026-10-01-283di: one side of a comparison as the script reads it — a URL, or a served directory and its page.</summary>
public sealed record BrowserCompareSide(string? Url, string? SiteDir, string Page)
{
    public static BrowserCompareSide Of(StagedSource staged) => new(staged.Url, staged.SiteDir, staged.Page);
}
