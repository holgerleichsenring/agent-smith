# Where configuration lives

The first version of the config studio wrote your edits back into `agentsmith.yml`. It seemed obvious at the time: one file, one truth, the UI is just a nicer editor. Then I ran it on Kubernetes, where the file is a mounted ConfigMap and mounted ConfigMaps are read only, and every save was a silent no-op. So the store of record moved into the database, and the file kept the one job only a file can do.

That leaves two ways Agent Smith gets its configuration, and which one you're in decides everything else on these pages.

Run it as a **server**, so docker-compose or Kubernetes or anything else long-lived, and the database is the store of record. You edit it in the Configuration studio in the dashboard, and the server picks the change up while it runs. Run it as the **CLI**, one shot at a time, and it reads `agentsmith.yml` from disk, the whole file, exactly as it always has.

These docs described the old world for longer than they should have. If you've been copying config blocks into a mounted ConfigMap and wondering why nothing happened, that's on me, and this page is the fix.

## What a server reads from the file

At boot the server reads exactly two blocks out of `agentsmith.yml`:

```yaml
persistence:
  provider: sqlite                 # sqlite | postgresql | mysql | sqlserver
  connection_string: "Data Source=/var/lib/agentsmith/agentsmith.db"

secrets:
  anthropic_api_key: ${ANTHROPIC_API_KEY}
  github_token:      ${GITHUB_TOKEN}
```

That's the bootstrap slice, and it's a chicken and egg thing. The connection to the database can't live in the database it describes, and the names of your secret environment variables have to be known before anything else loads. Everything after that (agents, trackers, connections, repos, projects, MCP servers, and the dozen global settings groups) comes out of the database.

So a server that boots with a full 400 line `agentsmith.yml` mounted at `/app/config/agentsmith.yml` will use two blocks of it and ignore the rest. The file isn't validated against what's in the database, and nothing warns you that the `projects:` block you just edited is inert. Put the catalog into the database instead, one of the two ways below.

## Getting an existing YAML into the database

If you already have a working `agentsmith.yml`, import it. Once:

```bash
agent-smith config import ./agentsmith.yml
```

The import is guarded. It refuses to run against a store that already has content unless you pass `--force`, because I wanted the destructive version of this to be something you type on purpose. `persistence:` is deliberately excluded from the import, since it stays in the file where the bootstrap can find it.

The studio has the same thing as a button (**Import agentsmith.yml**, top of every catalog page), and the other direction too:

```bash
agent-smith config export --output ./agentsmith-backup.yml
```

Export gives you back a file that round-trips through the real loader, which makes it a backup, a code review artifact, and the thing you hand to a second environment. Secrets come out as env var names, never values.

## Every edit is attributed and revertible

The studio writes a change record for each edit: who, when, which fields, old value to new value. The Changes view lists them newest first, and each one has a revert button.

![The Changes view, every config edit attributed and revertible](../assets/screenshots/config-changes.png)

The screenshot above is a fresh instance right after `agent-smith config import`, which is why every row says `by cli-import`. Edits from the studio carry the operator instead.

A config write also bumps a counter in Redis, and the running server watches it. Change a tracker's polling interval and the poller picks it up on its next cycle, with no restart in the loop. The exception is the bootstrap slice: change `persistence:` and you're restarting the process, by definition.

## Which surface owns which block

| Block | Server (docker / k8s) | CLI |
|---|---|---|
| `persistence:` | the file, read at boot | unused, since CLI runs are one-shot with no database |
| `secrets:` | the file, read at boot | the file |
| `agents:` `trackers:` `connections:` `repos:` `projects:` `mcp_servers:` | database, via the [Config studio](config-studio.md) | the file |
| `deployment:` `sandbox:` `orchestrator:` `queue:` `limits:` `skills:` `dialogue:` `registries:` `primary_provider:` `pipeline_cost_cap:` `pipeline_storage:` `pipeline_data_flow:` | database, via [Settings](settings.md) | the file |
| `pipeline_triggers:` | database, with no studio page yet, so import or export to edit it | the file |
| `trace:` | the file | the file |

The last two rows are honest exceptions. `pipeline_triggers:` is stored and served like everything else but has no catalog screen, so today you edit it by exporting, changing the block, and importing with `--force`. `trace:` never made it into the database taxonomy at all and is read from the file in both modes.

## Next

- [The Config studio](config-studio.md), the catalogs, the drawer, and what the studio refuses to let you save.
- [Settings](settings.md), the twelve global groups and what each one actually changes.
- [agentsmith.yml](yaml.md), the file itself: bootstrap, CLI, import/export, schema.
