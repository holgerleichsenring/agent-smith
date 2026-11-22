using AgentSmith.Contracts.Specs;
using AgentSmith.Infrastructure.Persistence.Entities;

namespace AgentSmith.Server.Services.Tickets;

/// <summary>2026-11-22-a720a: the cut a ticket's series reads, its specs and how many of them are left.</summary>
public sealed record SeriesLeft(RunSeriesCut? Cut, IReadOnlyList<SpecCutEntry> Entries, int Count);
