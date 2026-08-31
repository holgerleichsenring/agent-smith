using AgentSmith.Contracts.Providers;
using AgentSmith.Domain.Models;

namespace AgentSmith.Application.Services.Surface;

/// <summary>
/// 2026-08-30-c6ec: reduces the served description a run holds to the operations and
/// property names a client's usage can be compared against.
/// </summary>
public interface IServedSurfaceReader
{
    IReadOnlyList<ServedOperation> Read(SwaggerSpec spec);
}
