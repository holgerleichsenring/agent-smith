# Local Skill Iteration

Working on a skill or pattern locally — without rebuilding the agent-smith
binary, without releasing a new version of `agentsmith-skills`.

## Quick path: clone the catalog repo, point at it

```bash
# Clone the catalog repo somewhere outside the agent-smith working tree
git clone https://github.com/holgerleichsenring/agentsmith-skills /path/to/agentsmith-skills

# Point your local config at it
# agentsmith.yml:
skills:
  source: path
  path: /path/to/agentsmith-skills
```

Edits to `/path/to/agentsmith-skills/skills/**/SKILL.md` are picked up by the
next pipeline run — no rebuild, no restart needed (the loader reads SKILL.md
on each load).

## Variant: bind-mount in docker-compose

```yaml
# deploy/docker-compose.yml additions
services:
  agent-smith:
    volumes:
      - ../agentsmith-skills:/var/lib/agentsmith/skills:ro
    environment:
      AGENTSMITH_SKILLS_DIR: /var/lib/agentsmith/skills
```

Together with `skills.source: path` in `agentsmith.yml`, this lets you edit
SKILL.md on your host and have the container pick up the change immediately.

## Test runs

The test suite uses `./test-skills/` populated by `scripts/fetch-skills.sh`.
For local-iteration tests, point at your working copy instead:

```bash
export AGENTSMITH_TEST_SKILLS_DIR=/path/to/agentsmith-skills
dotnet test
```

## Releasing changes

Once the local iteration looks good:

1. Push your changes to the `agentsmith-skills` fork or branch.
2. Tag `vX.Y.Z`. The `release.yml` workflow builds the deterministic tarball
   and publishes the release.
3. Bump `skills.version` in your agent-smith production config to the new tag.
4. Roll the server. The new tag mismatches the cached `.pulled` marker and
   triggers a fresh pull.

Edge channel (`tag: edge`) tracks `main` and is rebuilt on every push — useful
for smoke-testing in a staging environment without minting a release.
