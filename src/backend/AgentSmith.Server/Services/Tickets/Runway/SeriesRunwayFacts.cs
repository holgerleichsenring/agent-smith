using AgentSmith.Application.Services.Specs;
using AgentSmith.Infrastructure.Persistence.Entities;

namespace AgentSmith.Server.Services.Tickets.Runway;

/// <summary>
/// 2026-10-13-8762v: what the runway is judged on, read once for the worker and the page alike.
/// </summary>
/// <param name="Attempts">The ticket's code attempts, oldest first (<see cref="TicketAttempts.Of"/>).</param>
/// <param name="Causes">Each attempt's recorded cause channel, by run id; a missing one is no series start.</param>
/// <param name="Newest">The newest code run that is not queued, a cancelled run with no step included.</param>
/// <param name="Queued">A capacity reservation of the ticket waits to run.</param>
/// <param name="HasCut">An attempt published a cut with specs.</param>
/// <param name="SpecsLeft">The latest cut's specs no attempt carried or finished.</param>
/// <param name="NewestDoneSpecs">The newest run finished at least one spec.</param>
/// <param name="PendingHoldingSeries">Pending defaults marked holds_series.</param>
/// <param name="Watermark">The ticket's operator-stop watermark (a cancel or a delete).</param>
/// <param name="RefusedMark">The worker's launch was refused after the newest run.</param>
/// <param name="ClosedMark">The worker found the ticket closed by a person after the newest run.</param>
public sealed record SeriesRunwayFacts(
    IReadOnlyList<Run> Attempts,
    IReadOnlyDictionary<string, string?> Causes,
    Run? Newest,
    bool Queued,
    bool HasCut,
    int SpecsLeft,
    bool NewestDoneSpecs,
    int PendingHoldingSeries,
    DateTimeOffset? Watermark,
    bool RefusedMark,
    bool ClosedMark,
    SeriesSettings Settings);
