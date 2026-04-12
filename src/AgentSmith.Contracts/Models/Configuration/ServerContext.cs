namespace AgentSmith.Contracts.Models.Configuration;

/// <summary>
/// Holds server-level context (e.g. config path) that webhook handlers
/// may need but that is only known at server startup time.
/// Registered as a singleton when running in server (webhook) mode.
/// </summary>
public sealed record ServerContext(string ConfigPath);
