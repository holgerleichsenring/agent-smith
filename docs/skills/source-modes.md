# Skill Catalog Source Modes

The agent-smith server resolves the skill catalog at boot from one of three
sources, configured under `skills:` in `agentsmith.yml`:

| Mode | Use case | What happens |
|---|---|---|
| `default` | Standard production deployment | Server pulls a versioned release from `holgerleichsenring/agent-smith-skills` and caches it in `cache_dir`. Re-pulls only if `version` changes. |
| `path` | Operator-managed mount (PVC, sidecar copy, GitOps) | Server validates the directory contains `skills/` and uses it as-is. No download. |
| `url` | Custom mirror or one-off override | Server pulls from an explicit URL with optional SHA256 verification. |

## `default`

```yaml
skills:
  source: default
  version: v1.0.0
  cache_dir: /var/lib/agentsmith/skills
  # sha256: <hex>    # optional: verify against a known release SHA
```

The release URL is `${repo}/releases/download/${version}/agentsmith-skills-${version}.tar.gz`.
Override the base repository for air-gap mirrors via the
`AGENTSMITH_SKILLS_REPOSITORY_URL` environment variable — see
[airgap.md](airgap.md).

The server writes a `.pulled` marker into `cache_dir` after a successful pull.
On restart, if the marker matches `version` and `skills/` exists, the pull is
skipped. Bumping `version` triggers a fresh pull.

## `path`

```yaml
skills:
  source: path
  path: /var/lib/agentsmith/skills
```

`path` must be a directory containing a `skills/` subtree (the structure the
agentsmith-skills release tarball expands into). The server validates this on
boot and fails fast if the layout is wrong. No download is attempted.

This is the right mode when the catalog is provisioned by something other than
the server itself — for example an ArgoCD sync hook that populates a PVC, or a
sidecar container that copies from a bundled image. See
[deploy/k8s/examples/path-mode-deployment.yaml](https://github.com/holgerleichsenring/agent-smith/blob/main/deploy/k8s/examples/path-mode-deployment.yaml).

## `url`

```yaml
skills:
  source: url
  url: https://example.com/internal/agentsmith-skills.tar.gz
  sha256: 3f1a8b…              # strongly recommended for non-default URLs
  cache_dir: /var/lib/agentsmith/skills
```

Pulls the tarball from the explicit URL. Use `sha256` to pin to a known build
— the server fails fast on hash mismatch. No version-based caching: every boot
re-pulls (use `path` if you want stable mounted catalogs).

## CLI parity

The same code path runs from the command line. With a config file present,
`pull` reads `skills.version` / `skills.url` / `skills.cache_dir` /
`skills.sha256` from `agentsmith.yml` and runs the same pull the server would
do at boot:

```bash
agentsmith skills pull --config agentsmith.yml
```

Any flag overrides its config counterpart:

```bash
agentsmith skills pull --config agentsmith.yml --version v1.1.0   # try a newer release
agentsmith skills pull --config agentsmith.yml --output /tmp/skills
agentsmith skills pull --version v1.0.0 --output ./test-skills    # no config file at all
agentsmith skills pull --url https://… --sha256 <hex> --output ./skills
```

`scripts/fetch-skills.sh` uses explicit `--version` and `--output` so CI works
without a project config file.
