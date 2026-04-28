# Custom Security Patterns

Pattern files (`*.yaml`) are consumed by the static scanner
([`StaticPatternScanner`](https://github.com/holgerleichsenring/agent-smith/blob/main/src/AgentSmith.Infrastructure/Services/Security/StaticPatternScanner.cs))
and the git-history scanner
([`GitHistoryScanner`](https://github.com/holgerleichsenring/agent-smith/blob/main/src/AgentSmith.Infrastructure/Services/Security/GitHistoryScanner.cs)).

Both scanners load the same YAML — there is no parallel hardcoded list.
Patterns in category `secrets` are additionally used by the git-history scanner;
all other categories run only against the working tree.

## Where patterns live

Default patterns ship as part of the
[agentsmith-skills](https://github.com/holgerleichsenring/agent-smith-skills)
release tarball — the same artefact the server pulls at boot for skills.
After a successful pull, the catalog cache (e.g. `/var/lib/agentsmith/skills`)
contains both:

```
{cacheDir}/
├── skills/                # role definitions (loaded by SkillLoader)
└── patterns/              # YAML regex pattern definitions (loaded by the scanners)
    ├── ai-security.yaml
    ├── api-auth.yaml
    ├── auth.yaml
    ├── compliance.yaml
    ├── config.yaml
    ├── injection.yaml
    ├── secrets.yaml
    └── ssrf.yaml
```

The pattern resolver looks at, in order:

1. `${AGENTSMITH_CONFIG_DIR}/patterns/` — operator override for custom or
   replacement patterns
2. `{catalogRoot}/patterns/` — patterns from the agentsmith-skills release
3. `./config/patterns/` (development convenience when running from a source
   checkout that still has bundled patterns)

The file name is organisational. The category exposed in findings comes from
the top-level `name:` field inside the YAML, falling back to the file name
if `name:` is omitted.

## Schema

```yaml
name: <category-name>     # category emitted in findings
patterns:
  - id: <opaque-id>       # required — see "ID stability" below
    regex: <pattern>      # required
    severity: <level>     # one of: info | low | medium | high | critical
    confidence: <0..10>   # detection confidence band
    cwe: <CWE-NNN>        # optional — Common Weakness Enumeration reference
    title: <short>        # human-readable headline
    description: <prose>  # detail shown in reports
    provider: <name>      # optional — surfaced as Finding.Provider
    revocationUrl: <url>  # optional — surfaced as Finding.RevokeUrl
```

`provider` and `revocationUrl` are optional metadata that flow straight into
findings. They are most useful for cloud-vendor secrets (AWS, GitHub, Stripe,
…) where the operator needs an immediate "go here to rotate this credential"
link. Patterns without a single canonical provider (generic JWT, generic
secret-assignment, placeholder strings) leave both fields unset.

## ID stability

Pattern IDs are **opaque identifiers**, not a public API:

- They are stable in practice — renaming an ID is a breaking change for anyone
  who has stored historical scan results that reference it.
- They are not used by code as a lookup key for behaviour. All metadata that
  scanners need (category, severity, provider, revocation URL) lives on the
  pattern definition itself.
- Tests never assert specific IDs; they assert behaviour (e.g. "this sample is
  flagged with severity=critical"). When you add a pattern, you don't need a
  matching test that hardcodes the new ID.

If you need to rename an ID, treat it like any other breaking metadata change
and bump documentation that mentions it.

## Adding a custom pattern

Two paths depending on whether the pattern is for everyone or just your
deployment:

**For everyone — contribute upstream:** open a PR against
[agentsmith-skills](https://github.com/holgerleichsenring/agent-smith-skills),
add or extend a YAML under `patterns/`. The next release ships it.

**For your deployment only — operator override:** point
`AGENTSMITH_CONFIG_DIR` at a directory that contains a `patterns/` subfolder
with your custom YAMLs. The resolver picks that location up before the
catalog. Mix-and-match isn't supported in this slice — when the override path
is set, the catalog patterns are not also loaded; copy any defaults you want
to keep into your override directory.

```yaml
# ${AGENTSMITH_CONFIG_DIR}/patterns/my-org.yaml
name: my-org
patterns:
  - id: internal-api-key
    regex: "MYORG-[A-Z0-9]{32}"
    severity: critical
    confidence: 9
    cwe: CWE-798
    title: "Internal API Key"
    description: "Hardcoded internal API key — must be rotated via vault."
    provider: "MyOrg Vault"
    revocationUrl: "https://vault.internal.myorg/keys"
```

## Where patterns flow

- Static scan → [`PatternFileMatcher`](https://github.com/holgerleichsenring/agent-smith/blob/main/src/AgentSmith.Infrastructure/Services/Security/PatternFileMatcher.cs)
  emits [`PatternFinding`](https://github.com/holgerleichsenring/agent-smith/blob/main/src/AgentSmith.Contracts/Models/PatternFinding.cs)s
  carrying `provider` and `revokeUrl` directly from the YAML.
- Git history scan → [`GitDiffSecretMatcher`](https://github.com/holgerleichsenring/agent-smith/blob/main/src/AgentSmith.Infrastructure/Services/Security/GitDiffSecretMatcher.cs)
  uses the same YAMLs filtered to `category=secrets`.
- SARIF / Markdown / JSON reports include `provider` and `revokeUrl` whenever
  the matched pattern provides them.
