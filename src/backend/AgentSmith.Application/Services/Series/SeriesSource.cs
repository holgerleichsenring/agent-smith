using AgentSmith.Contracts.Sandbox;

namespace AgentSmith.Application.Services.Series;

/// <summary>2026-11-27-3532c: one read-only scope the series master reads in a beat — the address its paths start with,
/// what it shows (a repository at a branch, or an upload), and the lazy scope behind it.</summary>
public sealed record SeriesSource(string Address, string Shows, ISourceScopeSandbox Scope);
