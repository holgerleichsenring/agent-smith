# Skills catalog

The skills — the role definitions that say "this is what an architect looks at, this is what a security reviewer asks, this is what a backend dev produces" — are authored in their own repository and versioned with release tags. Since p0325 every Agent Smith release **embeds** the skills catalog it was tested with: with no `skills:` block in `agentsmith.yml`, the embedded catalog materializes to disk at startup — no network fetch, no version pin. The `skills:` block is an **override** for skills development, mirrors, or running a different catalog version than the one embedded.

## Where they actually live

Repository: [`github.com/holgerleichsenring/agent-smith-skills`](https://github.com/holgerleichsenring/agent-smith-skills).

What's inside:

- A YAML manifest at the root (`catalog.yaml`) listing all skills and their roles.
- One directory per skill, with `SKILL.md` (the skill definition: frontmatter + role-specific prompts).
- A `concept-vocabulary.yaml` declaring the conceptual fields skills can activate against (`project_language`, `pipeline`, etc.).
- Tests for the activation logic and prompt rendering.

Release tags follow semver-ish (`v3.0.1`, `v3.1.0`, …). Every tag is a self-contained catalog you can pin.

## Why a separate repo

Two reasons:

- **The skills change faster than the framework.** A new skill (say `licence-compliance-reviewer` for legal-analysis) doesn't require an Agent Smith binary update. Tag the skills repo, pin `skills.version` in `agentsmith.yml`, and you have the new skill on the next run — without waiting for the next Agent Smith release (which will embed it).
- **The framework changes shouldn't break your skills mid-flight.** When the framework gets a new feature (a new tool, a new role), the skills repo opts in to it by adopting it in a new tag. The old tag keeps working with the old framework version. The two move independently.

That separation was painful to maintain when the skills lived in-tree — every skill edit was a binary rebuild and every binary release re-shipped the entire catalog.

## How you point Agent Smith at them

You usually don't. With no `skills:` block the embedded catalog is used. To override, set a `skills` block in `agentsmith.yml`:

```yaml
skills:
  version: v3.21.0             # pull this release tag instead of the embedded catalog
  cache_dir: /var/lib/agentsmith/skills
```

Resolution: an explicit `path` wins, then `url`, then `version`; only when none of them is set does the embedded catalog apply. **`source`** values (normally inferred from which field you set):

| Value | What it does |
|---|---|
| `embedded` | The catalog baked into the binary at build time. The default; needs no other fields. |
| `default` | Fetch the `version` tag from the upstream `github.com/holgerleichsenring/agent-smith-skills` releases. |
| `path` | Use a local directory (for in-house skills, or for dev work on the catalog itself). |
| `url` | Fetch a tarball from a URL (e.g. a private artefact registry). |

**`version`** is the release tag to fetch (setting it switches from embedded to the fetch flow); it is ignored for `path` (the path is the version).

**`cache_dir`** is where the framework unpacks the catalog at startup. Same value across orchestrator replicas is fine; the cache is read-only after first fetch. The cache is keyed by `version`, so bumping the pin pulls the new tag without re-fetching the old one.

## What resolution means in practice

At orchestrator startup (when a `version` override is set; the embedded default works the same way with the built-in tarball instead of a fetch):

1. Look at `skills.version` (e.g. `v3.0.1`).
2. Check if `${cache_dir}/v3.0.1/` exists.
3. If not, fetch it (clone the tag, extract the tarball, copy from the path).
4. Load `catalog.yaml` from that directory.
5. Validate that every skill referenced has a `SKILL.md`.

Runs then activate skills from this loaded catalog. Same activation, same role lookup, same prompt rendering — only the source location has moved.

## Bumping the catalog

Normally: upgrade Agent Smith — every release carries its own catalog. To run a newer catalog on the same binary, pin it explicitly:

```yaml
skills:
  version: v3.21.0     # override the embedded catalog
```

`docker compose restart orchestrator` or `kubectl rollout restart deployment/agent-smith-orchestrator -n agent-smith`. The new catalog gets fetched on the next start, loaded, validated, and the next run uses it.

If the new tag includes a breaking change to the concept vocabulary (renaming a concept, removing one), the validation step at startup fails fast with the diff between what your config references and what the catalog declares. Roll back to the previous tag, fix the config, re-deploy.

## Pre-pulling a catalog (`skills pull`)

For air-gapped hosts and image builds there's a CLI verb that does the fetch/extract step ahead of time:

```bash
agent-smith skills pull --version v3.21.0 --output /var/lib/agentsmith/skills
agent-smith skills pull --url https://artifacts.internal.example/agent-smith-skills.tar.gz --sha256 <digest>
```

`--version` and `--url` are mutually exclusive; both override the corresponding `skills:` config field, `--sha256` verifies the tarball, `--force` re-pulls over an existing cache. The extracted directory is exactly what a `skills: path:` override expects — pull once, mount read-only everywhere. See [Air-gap installs](../reference/skills/airgap.md) for the mirror patterns.

## Authoring skills (in-house)

For skills you don't want to upstream — a `legal-analysis` role specific to a contract template your team uses, or a `security-scan` role tuned for your stack — point `source` at a local path:

```yaml
skills:
  path: /etc/agent-smith/in-house-skills
```

Structure the local path the same way the upstream catalog is structured (a `catalog.yaml` at the root, one directory per skill). For mixing in-house and upstream skills, the cleanest approach today is to fork the upstream catalog and add your skills there. Multi-source merging is a planned feature, not a present one.

The `skill-manager` pipeline (in [Reference](../reference/pipelines/skill-manager.md)) can lint a local catalog and run it through the activation logic against a synthetic ticket — useful for testing a new skill before committing it.

## What the framework ships vs what the catalog ships

| Lives in the framework | Lives in the skills catalog |
|---|---|
| The orchestrator + sandbox-agent binaries | The skill definitions (`SKILL.md` files) |
| The pipeline presets (`fix-bug`, `add-feature`, …) | The role prompts (`as_lead`, `as_reviewer`, …) |
| The agent tools (`read_file`, `edit`, `grep_in_tree`, `web_fetch`, …) | The activation expressions per skill |
| The concept-type system | The concept vocabulary (`project_language`, `pipeline`, …) |
| The plan / review / verify / final phase machinery | Which skills run in which phase |

If you find yourself wanting to add a new role to a pipeline, that's a skills-catalog change. If you find yourself wanting a new tool the agent can call, or a new pipeline shape, that's a framework change.

## Next

- [Methodology](methodology.md) — what the roles defined here actually do.
- [Connect your AI provider](../connect-your-stuff/ai-providers.md) — the provider that runs the skill prompts.
- [Skills repository](https://github.com/holgerleichsenring/agent-smith-skills) — the source of truth.
