namespace AgentSmith.Contracts.Models.Configuration;

/// <summary>
/// The server's relational system-of-record. The server ALWAYS uses a DB +
/// Redis (Redis = transport/locks/nudges, DB = run history + the single-run
/// constraint) — there is one persistence path, not a toggle. The CLI one-shot
/// runs use neither. Provider is its own field (sqlite | postgresql | mysql |
/// sqlserver) so EF calls the matching Use{Sqlite|Npgsql|MySql|SqlServer}; the
/// default is a SQLite file (no service, no secrets → lives plainly in
/// agentsmith.yml, durable on a volume).
/// <para>
/// 2026-09-04-102b: a server-based connection string carries its credential inline, and
/// a ${name} placeholder inside it is expanded by NOTHING — this block is the bootstrap
/// slice, read before any secret map exists. A deployment that must keep the credential
/// out of its configuration file sets <see cref="Constants.PersistenceEnvKeys"/> instead:
/// both variables together replace this block wherever they are set.
/// </para>
/// </summary>
public sealed class PersistenceConfig
{
    public string Provider { get; init; } = "sqlite";

    public string ConnectionString { get; init; } = "Data Source=/var/lib/agentsmith/agentsmith.db";
}
