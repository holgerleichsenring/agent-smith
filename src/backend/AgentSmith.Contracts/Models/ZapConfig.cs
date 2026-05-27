namespace AgentSmith.Contracts.Models;

/// <summary>
/// Configuration for the OWASP ZAP scanner, loaded from config/zap.yaml.
/// </summary>
public sealed class ZapConfig
{
    public string DefaultScanType { get; set; } = "baseline";
    public int ContainerTimeout { get; set; } = 300;
}
