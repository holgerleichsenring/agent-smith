# Tool Configuration

Agent Smith's **api-scan** pipeline uses external security tools running in containers. Their behavior is controlled by YAML config files in the `config/` directory.

## nuclei.yaml

[Nuclei](https://github.com/projectdiscovery/nuclei) is a template-based vulnerability scanner. Agent Smith runs it against live API endpoints to detect security issues.

### Full Reference

```yaml
# config/nuclei.yaml

# Template tags to include (comma-separated)
tags: "api,auth,token,cors,ssl"

# Template tags to exclude (comma-separated)
exclude_tags: "dos,fuzz"

# Severity filter (comma-separated)
severity: "critical,high,medium,low"

# Per-request timeout in seconds
timeout: 10

# Number of retries per failed request
retries: 1

# Number of concurrent templates
concurrency: 10

# Max requests per second
rate_limit: 50

# Container-level timeout in seconds (kills the container after this)
container_timeout: 180
```

### Field Reference

| Field | Default | Description |
|-------|---------|-------------|
| `tags` | `api,auth,token,cors,ssl` | Nuclei template tags to include |
| `exclude_tags` | `dos,fuzz` | Template tags to skip (avoid destructive tests) |
| `severity` | `critical,high,medium,low` | Which severity levels to report |
| `timeout` | `10` | Per-request timeout (seconds) |
| `retries` | `1` | Retries per request |
| `concurrency` | `10` | Parallel template execution threads |
| `rate_limit` | `50` | Max requests per second |
| `container_timeout` | `180` | Hard kill timeout for the container (seconds) |

!!! warning
    Keep `exclude_tags: "dos,fuzz"` unless you are scanning a dedicated test environment. Fuzzing and DoS templates can disrupt production services.

### Tuning for CI

For CI pipelines with strict time limits, reduce the scope:

```yaml
tags: "api,auth,cors"
severity: "critical,high"
concurrency: 5
rate_limit: 20
container_timeout: 120
```

## spectral.yaml

[Spectral](https://docs.stoplight.io/docs/spectral/) is an OpenAPI linter. Agent Smith uses it to validate API specifications against OWASP security rules before the AI panel reviews findings.

### Full Reference

```yaml
# config/spectral.yaml

extends:
  - "https://unpkg.com/@stoplight/spectral-owasp-ruleset@2.0.1/dist/ruleset.mjs"

rules:
  # Override or disable specific rules:
  # owasp:api3:2023-no-additionalProperties: off
  # owasp:api4:2023-rate-limit: warn
```

### Field Reference

| Field | Description |
|-------|-------------|
| `extends` | Base rulesets to inherit. The OWASP ruleset covers API security best practices. |
| `rules` | Override individual rules: set to `off` to disable, `warn` or `error` to change severity. |

!!! tip
    The Spectral config file is mounted directly into the container as `.spectral.yaml`. Any valid Spectral configuration works here -- see the [Spectral docs](https://docs.stoplight.io/docs/spectral/) for the full rule format.

### Common Rule Overrides

```yaml
extends:
  - "https://unpkg.com/@stoplight/spectral-owasp-ruleset@2.0.1/dist/ruleset.mjs"

rules:
  # Disable if your API intentionally uses additionalProperties
  owasp:api3:2023-no-additionalProperties: off

  # Downgrade rate-limit check to warning (internal APIs)
  owasp:api4:2023-rate-limit: warn

  # Disable if you handle auth outside the OpenAPI spec
  owasp:api2:2023-no-api-keys-in-url: off
```

## Container Runtime

Both tools run inside containers managed by the `tool_runner` section in `agentsmith.yml`:

```yaml
tool_runner:
  type: auto                        # auto | docker | podman | process
  images:
    nuclei: projectdiscovery/nuclei:latest
    spectral: stoplight/spectral:6
```

The `auto` type checks for a Docker socket first, then Podman, and falls back to running the tools as local processes (requires them on `PATH`).

!!! note
    When running in Kubernetes, the tool runner uses the container runtime available in the pod. Set `type: docker` or `type: podman` explicitly if auto-detection does not work in your environment.
