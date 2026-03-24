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
- SSRF where only the path is user-controlled (not the host)

## Confidence Threshold

Every finding must include a confidence score (1-10).
Findings with confidence < 7 are discarded by the false-positive-filter.
When in doubt, do not report. Prefer underreporting to overreporting.

## Output Format

For each finding, include:
- severity: HIGH | MEDIUM | LOW
- owasp_category: e.g. API1:2023 — Broken Object Level Authorization
- endpoint: HTTP method + path (e.g. GET /api/v1/users/{id})
- title: short description (max 80 chars)
- description: detailed explanation with specific request/response reference
- confidence: 1-10

## Language

All output MUST be in English.
