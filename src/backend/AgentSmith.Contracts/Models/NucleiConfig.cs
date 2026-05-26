namespace AgentSmith.Contracts.Models;

/// <summary>
/// Configuration for the Nuclei scanner, loaded from config/nuclei.yaml.
/// </summary>
public sealed class NucleiConfig
{
    public string Tags { get; set; } = "api,auth,token,cors,ssl";
    public string ExcludeTags { get; set; } = "dos,fuzz";
    public string Severity { get; set; } = "critical,high,medium,low";
    public int Timeout { get; set; } = 10;
    public int Retries { get; set; } = 1;
    public int Concurrency { get; set; } = 10;
    public int RateLimit { get; set; } = 50;
    public int ContainerTimeout { get; set; } = 180;
}
