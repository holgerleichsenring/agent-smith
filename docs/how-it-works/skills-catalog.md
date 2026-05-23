# Skills catalog

The skills — the role definitions that say "this is what an architect looks at, this is what a security reviewer asks, this is what a backend dev produces" — don't live in the Agent Smith repository. They live in their own repository, versioned with release tags, pinned from your `agentsmith.yml`.

This page is the bit the older docs buried.

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

- **The skills change faster than the framework.** A new skill (say `licence-compliance-reviewer` for legal-analysis) doesn't require an Agent Smith binary update. Tag the skills repo, bump `skills.version` in `agentsmith.yml`, you have the new skill on the next run.
- **The framework changes shouldn't break your skills mid-flight.** When the framework gets a new feature (a new tool, a new role), the skills repo opts in to it by adopting it in a new tag. The old tag keeps working with the old framework version. The two move independently.

That separation was painful to maintain when the skills lived in-tree — every skill edit was a binary rebuild and every binary release re-shipped the entire catalog.

## How you point Agent Smith at them

The `skills` block in `agentsmith.yml`:

```yaml
skills:
  source: default              # or a path / URL — see below
  version: v3.0.1              # the release tag to pin
  cache_dir: /var/lib/agentsmith/skills
```

**`source`** values:

| Value | What it does |
|---|---|
| `default` | Fetch from the upstream `github.com/holgerleichsenring/agent-smith-skills` releases. |
| `path:/absolute/path` | Use a local directory (for in-house skills, or for dev work on the catalog itself). |
| `url:https://...` | Fetch a tarball from a URL (e.g. a private artefact registry). |

**`version`** is the release tag for `source: default`, ignored for `source: path:` (the path is the version), required as the asset filename pattern for `source: url:`.

**`cache_dir`** is where the framework unpacks the catalog at startup. Same value across orchestrator replicas is fine; the cache is read-only after first fetch. The cache is keyed by `version`, so bumping the pin pulls the new tag without re-fetching the old one.

## What "pinned" means in practice

At orchestrator startup:

1. Look at `skills.version` (e.g. `v3.0.1`).
2. Check if `${cache_dir}/v3.0.1/` exists.
3. If not, fetch it (clone the tag, extract the tarball, copy from the path).
4. Load `catalog.yaml` from that directory.
5. Validate that every skill referenced has a `SKILL.md`.

Runs then activate skills from this loaded catalog. Same activation, same role lookup, same prompt rendering — only the source location has moved.

## Bumping the catalog

```yaml
skills:
  source: default
  version: v3.1.0     # was v3.0.1
```

`docker compose restart orchestrator` or `kubectl rollout restart deployment/agent-smith-orchestrator -n agent-smith`. The new catalog gets fetched on the next start, loaded, validated, and the next run uses it.

If the new tag includes a breaking change to the concept vocabulary (renaming a concept, removing one), the validation step at startup fails fast with the diff between what your config references and what the catalog declares. Roll back to the previous tag, fix the config, re-deploy.

## Authoring skills (in-house)

For skills you don't want to upstream — a `legal-analysis` role specific to a contract template your team uses, or a `security-scan` role tuned for your stack — point `source` at a local path:

```yaml
skills:
  source: path:/etc/agent-smith/in-house-skills
  version: local
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
