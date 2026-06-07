namespace AgentSmith.Infrastructure.Persistence.Models;

/// <summary>
/// Two explicit fields: the provider and the native EF connection string for it.
/// Runtime default = sqlite + a file Data Source (no service, no secrets → lives
/// plainly in agentsmith.yml, durable across restart). Postgres/MySQL strings
/// carry credentials via ${secret} resolved before binding.
/// </summary>
public sealed record PersistenceOptions
{
    public PersistenceProvider Provider { get; init; } = PersistenceProvider.Sqlite;

    public string ConnectionString { get; init; } = "Data Source=agentsmith.db";
}
