# Phase 54: Security Scan Expansion

## Goal

Expand the security-scan pipeline with static pattern matching, git history
scanning, dependency auditing, and 4 new LLM specialist skills. Inspired by
ship-safe's 18-agent architecture — adapted to Agent Smith's pipeline model.

## Motivation

Agent Smith's current security-scan is purely LLM-based (5 skills analyzing
source diffs). This misses:

- **Deterministic patterns** that regex catches reliably (secrets, SSRF sinks,
  unsafe config) — LLMs sometimes miss these, and pattern-based pre-scanning
  reduces token cost while increasing recall
- **Git history secrets** — deleted from code but still in commit history
- **Dependency vulnerabilities** — known CVEs in third-party packages
- **Infrastructure config** — Dockerfile, K8s, Terraform, CI/CD misconfigurations
- **Supply chain attacks** — typosquatting, wildcard versions, suspicious install scripts
- **PII/compliance** — GDPR-relevant patterns in logging, responses, URLs
- **AI/LLM security** — prompt injection in code, unsafe output handling,
  OWASP LLM Top 10 and Agentic Top 10

Ship-safe covers all of this with 18 specialized agents. Agent Smith can achieve
similar coverage by combining deterministic tool commands (fast, cheap, reliable)
with LLM specialist skills (deep analysis, context-aware, low false positives).

## Architecture

### Pipeline Enhancement

The security-scan pipeline currently runs:

```
BootstrapProject → LoadContext → Triage → SkillRounds → Convergence → Compile → Deliver
```

New pipeline with tool commands feeding the LLM skills:

```
BootstrapProject → LoadContext
  → StaticPatternScan    ← NEW: regex patterns against source files
  → GitHistoryScan       ← NEW: secrets in git history
  → DependencyAudit      ← NEW: npm audit / pip-audit / dotnet audit
  → Triage → SkillRounds → Convergence → Compile → Deliver
```

The three new commands put their findings into PipelineContext. The LLM skills
receive them as structured input alongside the source code, giving them both
the deterministic findings AND the ability to reason about context.

### New Pipeline Commands

#### 1. StaticPatternScanCommand

In-process regex scanner. No Docker needed. Runs against all files in the repo
(respecting .gitignore and size limits).

Pattern categories (inspired by ship-safe):

**Secrets** (50+ patterns):
- AWS keys (`AKIA[0-9A-Z]{16}`)
- GitHub tokens (`ghp_`, `gho_`, `ghs_`)
- Stripe keys (`sk_live_`, `pk_live_`)
- Generic API keys, connection strings, private keys
- Placeholder secrets ("your_key_here", "CHANGE_ME", "sk-xxx")

**SSRF Sinks**:
- User input in `fetch()`, `axios`, `http.get()`, `requests.get()`
- URL template literals with interpolation
- Cloud metadata endpoints (169.254.169.254)

**Injection Sinks**:
- `eval()`, `new Function()`, `exec()`, `spawn()` with user input
- `dangerouslySetInnerHTML`, `innerHTML`, `v-html`
- SQL string concatenation/template literals
- `pickle.loads()`, `yaml.load()` without SafeLoader

**Config Misconfigurations**:
- Dockerfile: `:latest` tags, secrets in ENV/ARG, no USER instruction
- K8s: privileged containers, no resource limits, `:latest` images
- Terraform: public S3, wildcard IAM, open security groups
- CI/CD: unpinned GitHub Actions, `pull_request_target` with checkout,
  script injection via `${{ github.event }}`, secrets in echo

**Infrastructure**:
- CORS wildcard with credentials
- Debug mode in production
- Missing security headers
- `NODE_TLS_REJECT_UNAUTHORIZED=0`

Output: List of `PatternFinding` objects with file, line, pattern name,
severity, confidence, CWE mapping.

Implementation: New `StaticPatternScanner` service in Infrastructure.
Pattern definitions in YAML files under `config/patterns/` (extensible).
Uses `IFileDiscovery` to walk the repo, respects `.gitignore`.

#### 2. GitHistoryScanCommand

Runs `git log --all -p --diff-filter=D` to find secrets that were committed
and later deleted. These are MORE dangerous than active secrets because
developers think they're removed.

Implementation: Uses LibGit2Sharp (already a dependency) to walk commit
history. Applies the same secret patterns from StaticPatternScan.
Compares against current working tree — if secret is history-only,
elevates severity to CRITICAL.

Output: List of `HistoryFinding` with commit hash, file, line, pattern,
whether secret still exists in working tree.

#### 3. DependencyAuditCommand

Language-agnostic dependency vulnerability scanner. Detects the package
manager and runs the appropriate audit tool:

| Ecosystem | Detection | Audit Command |
|-----------|-----------|---------------|
| npm/yarn/pnpm | package.json | `npm audit --json` |
| Python | requirements.txt / pyproject.toml | `pip-audit --format=json` |
| .NET | *.csproj | `dotnet list package --vulnerable --format json` |
| Go | go.mod | `govulncheck -json ./...` |
| Ruby | Gemfile | `bundle-audit check --format json` |

Plus structural checks (no Docker needed):
- Missing lockfile
- Wildcard versions (`"*"`)
- Git/URL dependencies (bypass registry integrity)
- Typosquatting detection (Levenshtein distance ≤2 from popular packages)

Implementation: Runs via `ProcessToolRunner` (local) or `DockerToolRunner`
if the audit tool isn't installed. Parses JSON output into `DependencyFinding`
objects with CVE, severity, package, version, fix version.

### New LLM Specialist Skills

#### 4. config-auditor.yaml

Specializes in infrastructure and configuration security:
- Dockerfile, Docker Compose, Kubernetes manifests
- Terraform, CloudFormation
- CI/CD pipelines (GitHub Actions, GitLab CI, Azure DevOps)
- Web server config (nginx, Apache)
- Framework config (Next.js, Django, Flask settings)

Reviews StaticPatternScan findings for config category and adds
context-aware analysis (e.g., "this `:latest` tag is in a dev-only
compose file, lower severity").

Triggers: `dockerfile`, `kubernetes`, `terraform`, `ci-cd`, `configuration`

#### 5. supply-chain-auditor.yaml

Specializes in dependency and supply chain security:
- Reviews DependencyAudit findings
- Analyzes package.json/requirements.txt structure
- Typosquatting risk assessment
- Install script analysis
- Dependency confusion risk (scoped packages, private registries)
- Unused dependencies (larger attack surface)

Triggers: `dependencies`, `package-json`, `requirements`, `supply-chain`

#### 6. compliance-checker.yaml

Specializes in PII, privacy, and compliance:
- PII in logging (email, SSN, credit card, phone in console.log/logger)
- PII in error responses (stack traces, user objects)
- PII in URLs (query parameters logged in access logs)
- PII sent to third-party analytics without consent
- GDPR Article 17 (right to deletion — user model without delete endpoint)
- Unencrypted PII storage
- IP address logging without anonymization
- Tracking scripts without consent mechanism

Triggers: `logging`, `pii`, `gdpr`, `compliance`, `privacy`, `analytics`

#### 7. ai-security-reviewer.yaml

Specializes in AI/LLM and agentic security (OWASP LLM Top 10 + Agentic Top 10):

LLM Security:
- Prompt injection (user input concatenated into prompts)
- LLM output in eval/SQL/HTML (code execution via prompt injection)
- Missing max_tokens limits
- Hardcoded system prompts exposed to client
- RAG pipeline without input sanitization

Agentic Security:
- Tools with write/delete/exec without human confirmation
- Agent execution without iteration/timeout limits
- Memory poisoning (user input written to persistent memory)
- Missing audit logging for tool invocations
- Multi-agent chains without privilege isolation

MCP Security:
- MCP servers without authentication
- Tool arguments in SQL/eval/file paths
- Overprivileged tool permissions

Triggers: `llm`, `ai`, `agent`, `mcp`, `rag`, `prompt`, `langchain`, `openai`

### Updated security-scan Pipeline Preset

```csharp
public static readonly IReadOnlyList<string> SecurityScan =
[
    CommandNames.BootstrapProject,
    CommandNames.LoadContext,
    CommandNames.StaticPatternScan,    // NEW
    CommandNames.GitHistoryScan,       // NEW
    CommandNames.DependencyAudit,      // NEW
    CommandNames.LoadSkills,
    CommandNames.Triage,
    // ... dynamic SkillRound insertion by triage ...
    CommandNames.ConvergenceCheck,
    CommandNames.CompileFindings,
    CommandNames.DeliverFindings,
];
```

### Existing Skill Updates

**injection-checker.yaml** — Add explicit SSRF patterns to the rules.
Reference StaticPatternScan SSRF findings for confirmation.

**secrets-detector.yaml** — Reference GitHistoryScan findings. Focus LLM
analysis on context (is this a test file? is the secret rotated?) rather
than pattern detection (handled by static scan).

**vuln-analyst.yaml** — Reference DependencyAudit CVE findings. Focus on
impact assessment rather than vulnerability detection.

### Pattern Definition Format

Extensible YAML files under `config/patterns/`:

```yaml
# config/patterns/secrets.yaml
name: secrets
patterns:
  - id: aws-access-key
    regex: "AKIA[0-9A-Z]{16}"
    severity: critical
    confidence: 9
    cwe: CWE-798
    title: "AWS Access Key ID"
    description: "Hardcoded AWS access key found"

  - id: github-token
    regex: "ghp_[A-Za-z0-9]{36}"
    severity: critical
    confidence: 9
    cwe: CWE-798
    title: "GitHub Personal Access Token"
```

```yaml
# config/patterns/injection.yaml
name: injection
patterns:
  - id: eval-user-input
    regex: "eval\\s*\\(.*\\b(req|request|params|query|body|input|args)\\b"
    severity: high
    confidence: 7
    cwe: CWE-95
    title: "Code injection via eval()"
```

This makes patterns extensible without code changes — users can add
project-specific patterns.

## Files to Create

### Commands & Handlers
- `AgentSmith.Contracts/Commands/CommandNames.cs` — add 3 new command names
- `AgentSmith.Application/Services/Handlers/StaticPatternScanHandler.cs`
- `AgentSmith.Application/Services/Handlers/GitHistoryScanHandler.cs`
- `AgentSmith.Application/Services/Handlers/DependencyAuditHandler.cs`
- `AgentSmith.Application/Models/StaticPatternScanContext.cs`
- `AgentSmith.Application/Models/GitHistoryScanContext.cs`
- `AgentSmith.Application/Models/DependencyAuditContext.cs`

### Infrastructure Services
- `AgentSmith.Infrastructure/Services/Security/StaticPatternScanner.cs`
- `AgentSmith.Infrastructure/Services/Security/PatternDefinitionLoader.cs`
- `AgentSmith.Infrastructure/Services/Security/GitHistoryScanner.cs`
- `AgentSmith.Infrastructure/Services/Security/DependencyAuditor.cs`
- `AgentSmith.Contracts/Services/IStaticPatternScanner.cs`
- `AgentSmith.Contracts/Services/IGitHistoryScanner.cs`
- `AgentSmith.Contracts/Services/IDependencyAuditor.cs`
- `AgentSmith.Contracts/Models/PatternFinding.cs`
- `AgentSmith.Contracts/Models/HistoryFinding.cs`
- `AgentSmith.Contracts/Models/DependencyFinding.cs`

### Pattern Definitions
- `config/patterns/secrets.yaml`
- `config/patterns/injection.yaml`
- `config/patterns/ssrf.yaml`
- `config/patterns/config.yaml`
- `config/patterns/compliance.yaml`
- `config/patterns/ai-security.yaml`

### Skills
- `config/skills/security/config-auditor.yaml`
- `config/skills/security/supply-chain-auditor.yaml`
- `config/skills/security/compliance-checker.yaml`
- `config/skills/security/ai-security-reviewer.yaml`

## Files to Modify

- `AgentSmith.Contracts/Commands/PipelinePresets.cs` — add commands to security-scan
- `AgentSmith.Contracts/Commands/CommandNames.cs` — new command names
- `AgentSmith.Infrastructure/DependencyInjection.cs` — register new services
- `config/skills/security/injection-checker.yaml` — add SSRF, reference static findings
- `config/skills/security/secrets-detector.yaml` — reference history findings
- `config/skills/security/vuln-analyst.yaml` — reference dependency findings

## Implementation Order

1. Pattern model + loader (PatternFinding, PatternDefinitionLoader)
2. StaticPatternScanCommand + handler + scanner service
3. Pattern YAML files (secrets, injection, ssrf, config, compliance, ai-security)
4. GitHistoryScanCommand + handler + scanner service
5. DependencyAuditCommand + handler + auditor service
6. 4 new skill YAML files
7. Update existing skills to reference tool findings
8. Pipeline preset update
9. Tests

## Definition of Done

- [ ] StaticPatternScan runs against repo, produces findings with file/line/severity/CWE
- [ ] Pattern definitions loaded from YAML, extensible without code changes
- [ ] GitHistoryScan detects secrets in commit history via LibGit2Sharp
- [ ] History-only secrets marked as CRITICAL
- [ ] DependencyAudit detects package manager and runs appropriate audit
- [ ] At least npm audit and pip-audit supported
- [ ] 4 new LLM skills (config-auditor, supply-chain, compliance, ai-security)
- [ ] Existing skills reference tool findings in their analysis
- [ ] security-scan pipeline includes all 3 new commands before triage
- [ ] All commands gracefully skip if not applicable (no npm → skip DependencyAudit)
- [ ] Pattern definitions for 50+ secret patterns, 20+ injection patterns
- [ ] OWASP mapping (CWE, Top 10 category) on all pattern findings
- [ ] Tests for pattern matching, history scanning, dependency parsing
- [ ] `dotnet build` + `dotnet test` clean

## Dependencies

- LibGit2Sharp (already present — used for git history scanning)
- No new NuGet packages needed for static pattern scanning
- ProcessToolRunner (already present — used for npm audit, pip-audit)
