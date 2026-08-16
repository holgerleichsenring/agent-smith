using AgentSmith.Contracts.Commands;
using AgentSmith.Domain.Models;

namespace AgentSmith.Application.Services.Scans;

/// <summary>p0429: what the scan's ratified criteria were actually answered by.</summary>
public interface IScanCoverageAccountant
{
    SpecAccount Account(PipelineContext pipeline);
}
