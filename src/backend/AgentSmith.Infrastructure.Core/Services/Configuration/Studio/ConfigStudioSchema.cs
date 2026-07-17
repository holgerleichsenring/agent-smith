namespace AgentSmith.Infrastructure.Core.Services.Configuration.Studio;

/// <summary>
/// The relational schema for the DB-backed config catalog. Referential integrity
/// is enforced by construction: <c>projects</c> and <c>project_repos</c> carry
/// FKs to the catalog tables, so an unknown agent/tracker/repo ref cannot be
/// inserted — the same guarantee <see cref="AgentSmith.Contracts.Services.ConfigReferentialValidator"/>
/// gives at the application layer and the UI gives with FK pickers. The
/// <c>config_changes</c> table is the versioned, attributed audit trail (who /
/// when / target / diff) with a revert flag, present from day one.
/// Written portably (works on SQLite and PostgreSQL); the live migration wiring
/// is the follow-up p0346 (relational runtime).
/// </summary>
public static class ConfigStudioSchema
{
    public const string Ddl = """
        CREATE TABLE IF NOT EXISTS agents (
            id          TEXT PRIMARY KEY,
            provider    TEXT NOT NULL,
            models_json TEXT NOT NULL,
            key_secret  TEXT NULL
        );

        CREATE TABLE IF NOT EXISTS trackers (
            id          TEXT PRIMARY KEY,
            type        TEXT NOT NULL,
            org         TEXT NULL,
            project     TEXT NULL,
            auth_secret TEXT NULL
        );

        CREATE TABLE IF NOT EXISTS repos (
            id     TEXT PRIMARY KEY,
            name   TEXT NOT NULL,
            branch TEXT NULL
        );

        CREATE TABLE IF NOT EXISTS connections (
            id             TEXT PRIMARY KEY,
            type           TEXT NOT NULL,
            organization   TEXT NULL,
            project        TEXT NULL,
            auth_secret    TEXT NULL,
            default_branch TEXT NULL
        );

        CREATE TABLE IF NOT EXISTS mcp_servers (
            id          TEXT PRIMARY KEY,
            transport   TEXT NOT NULL,
            url         TEXT NULL,
            auth_secret TEXT NULL
        );

        CREATE TABLE IF NOT EXISTS secrets (
            id TEXT PRIMARY KEY
        );

        CREATE TABLE IF NOT EXISTS projects (
            id          TEXT PRIMARY KEY,
            agent_id    TEXT NOT NULL REFERENCES agents(id),
            tracker_id  TEXT NOT NULL REFERENCES trackers(id),
            trigger     TEXT NULL,
            pipelines_json TEXT NOT NULL DEFAULT '[]'
        );

        CREATE TABLE IF NOT EXISTS project_repos (
            project_id TEXT NOT NULL REFERENCES projects(id) ON DELETE CASCADE,
            repo_id    TEXT NOT NULL REFERENCES repos(id),
            PRIMARY KEY (project_id, repo_id)
        );

        CREATE TABLE IF NOT EXISTS config_changes (
            id          TEXT PRIMARY KEY,
            version     INTEGER NOT NULL,
            ts          TEXT NOT NULL,
            actor       TEXT NOT NULL,
            entity_type TEXT NOT NULL,
            entity_id   TEXT NOT NULL,
            operation   TEXT NOT NULL,
            before_json TEXT NULL,
            after_json  TEXT NULL,
            reverted    INTEGER NOT NULL DEFAULT 0
        );
        """;
}
