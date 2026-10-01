namespace AgentSmith.Application.Services.Browser;

/// <summary>2026-10-01-283de: one request a render could not complete, or that the egress guard refused, and why.</summary>
public sealed record BrowserRequestNote(string Url, string Reason);
