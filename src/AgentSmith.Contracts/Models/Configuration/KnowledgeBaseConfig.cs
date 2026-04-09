namespace AgentSmith.Contracts.Models.Configuration;

/// <summary>
/// Configuration for the project knowledge base compilation.
/// </summary>
public sealed class KnowledgeBaseConfig
{
    public int CompileIntervalMinutes { get; set; } = 60;
    public bool CompileOnEveryRun { get; set; }
    public string CompileModel { get; set; } = "haiku";
}
