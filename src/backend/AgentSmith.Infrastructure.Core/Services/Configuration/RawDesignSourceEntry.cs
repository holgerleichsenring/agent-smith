using AgentSmith.Contracts.Models.Configuration;

namespace AgentSmith.Infrastructure.Core.Services.Configuration;

/// <summary>
/// 2026-10-01-7f7aa: raw YAML shape for one entry inside the top-level <c>design_sources:</c>
/// catalog. <c>Auth</c> holds a secret NAME from <c>secrets:</c>, never a value.
/// </summary>
public sealed class RawDesignSourceEntry
{
    public DesignSourceVendor Vendor { get; set; }
    public string Auth { get; set; } = string.Empty;
    public string? DisplayName { get; set; }
}
