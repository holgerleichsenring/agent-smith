# Air-gap Skill Catalog

Two patterns work in disconnected environments. Pick the one that matches how
your platform team distributes artefacts.

## Pattern 1: Mirror the default URL

Mirror the GitHub release layout on an internal host, then point the server at
it via env var:

```yaml
# agentsmith.yml — production config
skills:
  source: default
  version: v1.0.0
  sha256: 3f1a8b…    # pin against tampering
  cacheDir: /var/lib/agentsmith/skills
```

```bash
# Server env (Helm value, K8s env, docker-compose env_file)
AGENTSMITH_SKILLS_REPOSITORY_URL=https://internal-mirror.example.com/agentsmith-skills
```

The server constructs the URL as:
```
https://internal-mirror.example.com/agentsmith-skills/releases/download/v1.0.0/agentsmith-skills-v1.0.0.tar.gz
```

Your mirror needs to expose that exact path layout. Copying the release
artefacts (`.tar.gz` + `.sha256`) into the matching directory tree is enough.

See [deploy/k8s/examples/airgap-deployment.yaml](../../deploy/k8s/examples/airgap-deployment.yaml).

## Pattern 2: Pre-populate the cache

Pull the catalog in a connected environment, copy the directory across the
boundary, mount as `path`:

```bash
# Connected side
agentsmith skills pull --version v1.0.0 --output ./skills-bundle

# Transfer (rsync, USB, S3 with offline upload, etc.)
# Disconnected side
mv ./skills-bundle /var/lib/agentsmith/skills
```

```yaml
skills:
  source: path
  path: /var/lib/agentsmith/skills
```

No network calls happen at boot. Updating the catalog means re-running the
copy.

## Verifying integrity

Both patterns support `sha256:` in `agentsmith.yml`. In an air-gap context this
is the recommended way to detect:

- Tampered mirror artefacts (Pattern 1)
- Damaged transport (Pattern 2 — the catalog is verified on every server pull,
  but you can also verify the tarball with `sha256sum -c` before copying it
  across the boundary)

Get the hash from the official release sidecar
(`agentsmith-skills-vX.Y.Z.tar.gz.sha256`) and pin it in your config.
