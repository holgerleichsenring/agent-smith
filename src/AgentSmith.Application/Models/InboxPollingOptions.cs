namespace AgentSmith.Application.Services.Triggers;

/// <summary>
/// Configuration for the inbox polling service.
/// </summary>
public sealed class InboxPollingOptions
{
    public string InboxPath { get; set; } = "./inbox";
    public string ProcessingPath { get; set; } = "./processing";
    public string OutboxPath { get; set; } = "./outbox";
    public string ArchivePath { get; set; } = "./archive";
    public int PollIntervalSeconds { get; set; } = 5;
}
