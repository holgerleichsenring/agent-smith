namespace AgentSmith.Server.Models;

/// <summary>2026-11-24-f9c5f: one repo's final pull request of a series — the series branch into the default branch — as the
/// series' last beat saw it: its link and number and what the series said about it last.</summary>
public sealed record SeriesRunDeliveryView(string Repo, string Url, string Number, string? Said);
