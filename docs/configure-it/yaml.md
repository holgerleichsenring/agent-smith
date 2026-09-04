# agentsmith.yml

The file didn't go away. It changed jobs. Depending on how you run Agent Smith it's the bootstrap for a server, the entire configuration for the CLI, or the artifact you move configuration around in.

## As a server bootstrap

A server reads `persistence:` and `secrets:` out of this file at boot and nothing else. Keep it that small:

```yaml
persistence:
  provider: postgresql
  connection_string: "Host=postgres;Database=agentsmith;Username=agentsmith;Password=…"

secrets:
  anthropic_api_key: ${ANTHROPIC_API_KEY}
  github_token:      ${GITHUB_TOKEN}
```

The `${...}` references resolve from the process environment. Values never belong in this file. Agent Smith refuses a config that carries raw secrets.

`persistence:` is the exception, and it cuts the other way: it is read *before* the secrets map exists, so a `${...}` inside `connection_string` is expanded by nothing and would be sent to the database as the password itself. A deployment that must keep the credential out of this file sets the pair

```
AGENTSMITH_PERSISTENCE_PROVIDER=postgresql
AGENTSMITH_PERSISTENCE_CONNECTION=Host=postgres;Database=agentsmith;Username=agentsmith;Password=…
```

which replaces the whole `persistence:` block wherever both are set — in the server and in the `database migrate` container alike, which is why they have to be set on both. Set one without the other and neither is used: the provider decides how the connection string is parsed, so half a pair is reported as a blocking startup finding and the file's block keeps running.

The server looks for the file at `CONFIG_PATH`, and the container images default that to `/app/config/agentsmith.yml`. If the file is missing entirely the server still boots, on a SQLite default with no secret names, and tells you so in its startup findings rather than dying silently.

## As the CLI's whole configuration

The CLI is a different animal. One shot runs, with no database and no Redis behind them. It reads the entire file (agents, repos, trackers, projects, the lot), and that hasn't changed and isn't going to.

The CLI looks for `agentsmith.yml` in the working directory, then `./config/agentsmith.yml`, then your home directory. `--config /path/to/agentsmith.yml` overrides all of it.

```bash
agent-smith doctor --config ./agentsmith.yml
agent-smith fix --ticket 54 --project todolist
```

So a laptop that drives runs from the CLI and a server that runs the same project from a tracker are configured in two different places on purpose. If you want them to agree, export from the server and use that file for the CLI.

## As the import/export artifact

```bash
# database -> file, for backup, review, or seeding a second environment
agent-smith config export --output ./agentsmith-backup.yml

# file -> database, guarded
agent-smith config import ./agentsmith.yml
agent-smith config import ./agentsmith.yml --force   # overwrite a non-empty store
```

Import refuses a store that already holds configuration unless you pass `--force`, and `persistence:` is excluded in both directions, because it belongs to the file and importing it into the database it describes would be nonsense.

Export round-trips: the YAML that comes out loads back through the same loader that reads a CLI config, which is what makes it usable as a real backup rather than a pretty-printed summary.

There's a third command worth knowing before a rollout:

```bash
agent-smith config validate
```

It reports what the server would report about this configuration, without starting a server, and exits non-zero on anything blocking. That makes it gateable in CI.

## Editor support

The repo ships a JSON schema. Point your editor at it and you get completion and inline errors for the whole file:

```yaml
# yaml-language-server: $schema=https://raw.githubusercontent.com/holgerleichsenring/agent-smith/main/config/agentsmith.schema.json
```

The annotated example at `config/agentsmith.example.yml` in the repo is the fullest reference there is, every block with comments about why it exists. It's also a legitimate thing to import into a fresh instance, just to see the studio with content in it.

## Field reference

The block-by-block field reference lives in [agentsmith.yml reference](../reference/configuration/agentsmith-yml.md), and the generated schema in [agentsmith.yml schema](../reference/configuration/agentsmith-yml-schema.md). Both describe the file format, which is shared by all three jobs above. What differs is only who reads which part.
