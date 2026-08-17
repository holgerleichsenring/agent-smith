using AgentSmith.Domain.Models;

namespace AgentSmith.Application.Services.Scans;

/// <summary>p0429: what a named scan pipeline states it is looking for.</summary>
public interface IScanContractCatalogue
{
    ScanContract For(string? pipelineName);
}
