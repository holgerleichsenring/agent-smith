# API Security Analysis Principles

These rules govern all security skill roles in the API security scan pipeline.

## Scope

This analysis identifies high-confidence security vulnerabilities in REST and GraphQL APIs.
It is grounded in the OWASP API Security Top 10 (2023) and focuses on findings from
Nuclei scans and OpenAPI/Swagger schema reviews.
It is a pre-review aid — not a replacement for professional penetration testing.

## OWASP API Security Top 10 (2023) Reference

- API1:2023 — Broken Object Level Authorization (BOLA)
- API2:2023 — Broken Authentication
- API3:2023 — Broken Object Property Level Authorization
- API4:2023 — Unrestricted Resource Consumption
- API5:2023 — Broken Function Level Authorization
- API6:2023 — Unrestricted Access to Sensitive Business Flows
- API7:2023 — Server Side Request Forgery (SSRF)
- API8:2023 — Security Misconfiguration
- API9:2023 — Improper Inventory Management
- API10:2023 — Unsafe Consumption of APIs

## Exclusions

Do NOT flag the following (low signal-to-noise ratio):
- Denial of service (DoS) or rate limiting without demonstrated exploit path
- Race conditions without reproducible evidence
- Source code analysis (this pipeline operates on HTTP traffic and schemas only)
- Infrastructure-level issues (TLS cipher suites, network topology, firewall rules)
- Missing security headers without a concrete exploit scenario
- Informational findings without actionable remediation
- SSRF where only the path is user-controlled (not the host or protocol)
- Missing audit logs — not a vulnerability
- Regex injection / regex DoS — not a vulnerability in this context
- Outdated libraries — managed separately by dependency scanning

## API-Specific Precedents

These rules override general analysis for API security scans:

1. **Environment variables and CLI arguments**: Treated as trusted input. Attacks
   requiring control of env vars or CLI flags are invalid.
2. **UUIDs in paths/params**: Assumed unguessable. Do not report missing UUID validation
   or enumeration concerns for UUID-based identifiers.
3. **Client-side validation**: Not a vulnerability. The API is responsible for all
   input validation — missing client-side checks are not reportable.
4. **URL logging**: Assumed safe — URLs in logs are not a vulnerability.
5. **Logging non-PII data**: Not a vulnerability. Only report if secrets, passwords,
   or personally identifiable information (PII) are logged in API responses or server logs.
6. **AI/LLM endpoints**: Including user-controlled content in AI prompts is not a
   vulnerability in this context (prompt injection is outside API security scan scope).
7. **Subtle web vulnerabilities**: Do not report tabnabbing, XS-Leaks, prototype
   pollution, or open redirects unless extremely high confidence with demonstrated
   impact on the API.
8. **CORS misconfiguration**: Only report if the Access-Control-Allow-Origin is
   concretely exploitable (wildcard with credentials, or reflects arbitrary origin).
   Permissive CORS without credentials is informational, not a vulnerability.

## Confidence Threshold

Every finding must include a confidence score (0-100).
See observation-schema.md for the confidence calibration table.
Findings in the Low band (< 30) are discarded unconditionally.
Findings in the Medium band (30-69) require clearly articulated exploitation conditions.
When in doubt, do not report. Prefer underreporting to overreporting.

## Output Format

For each finding, include:
- severity: HIGH | MEDIUM | LOW
- owasp_category: e.g. API1:2023 — Broken Object Level Authorization
- endpoint: HTTP method + path (e.g. GET /api/v1/users/{id})
- title: short description (max 80 chars)
- description: detailed explanation with specific request/response reference
- confidence: 1-10

## Code-Aware Analysis (when source is available)

When `api_source_available: true`, three additional skills run alongside the
attacker-perspective contributors:

- `auth-config-reviewer` — reviews authentication configuration in source
- `ownership-checker` — confirms ownership/tenant predicates on state-changing routes
- `upload-validator-reviewer` — reviews file-upload handler bodies

### Evidence modes

Findings carry one of three evidence modes:

- `potential` — inferred from schema or static patterns only
- `confirmed` — confirmed by an authenticated HTTP probe response
- `analyzed_from_source` — backed by a direct read of the handler body or config block

### File-line attribution

When `evidence_mode = analyzed_from_source`, the finding MUST include a
`location` of the form `relative/path/file.ext:line`. Routes whose
`RouteHandlerLocation.Confidence < 0.5` MUST NOT yield findings — log the route
as low-confidence and stop.

### Boundary with attacker skills

Code-aware skills and attacker-perspective skills (anonymous-attacker,
low-privilege-attacker, idor-prober, input-abuser, response-analyst) are
complementary, not redundant. A finding may legitimately appear from both
sides; the chain-analyst is responsible for cross-mode chains.

## Language

All output MUST be in English.
