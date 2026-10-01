namespace AgentSmith.Application.Services.Browser;

/// <summary>
/// 2026-10-01-283de: one selector's computed style as the browser computed it — for the first of
/// <see cref="Count"/> matches; a count of zero is a selector that matched nothing.
/// </summary>
public sealed record BrowserStyleRow(string Selector, int Count, IReadOnlyDictionary<string, string>? Values);
