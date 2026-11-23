namespace AgentSmith.Server.Models;

/// <summary>2026-11-23-933da: where a target keeps PoC sources — shown on the PoCs page's target strip.</summary>
public sealed record PocTargetView(string Name, string SourceRepo, string Root);
