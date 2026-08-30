using AgentSmith.Domain.Models;

namespace AgentSmith.Application.Services.Surface;

/// <summary>
/// 2026-08-30-c6ec: computes what the served interface offers that no first-party client
/// was found to exercise.
/// </summary>
public interface ISurfaceDifferenceCalculator
{
    SurfaceDifferenceReport Compute(
        IReadOnlyList<ServedOperation> served, ClientUsageReport usage, string catalogueVersion);
}
