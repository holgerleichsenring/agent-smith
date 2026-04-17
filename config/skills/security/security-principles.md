# Security Analysis Principles

These rules govern all security skill roles in Agent Smith.

## Scope

This analysis identifies high-confidence security vulnerabilities in code changes.
It is a pre-review aid — not a replacement for professional penetration testing.

## Exclusions

Do NOT flag the following (low signal-to-noise ratio):
- Denial of service (DoS) via resource exhaustion
- Log spoofing / log injection
- Theoretical race conditions without demonstrated impact
- Memory safety issues in managed languages (.NET, Java, Go)
- Issues only in test files (test-only code paths)
- SSRF where only the path is user-controlled (not host or protocol)
- Missing security headers without demonstrated exploit path
- Informational findings without actionable remediation
- Missing audit logs — not a vulnerability
- Insecure documentation (markdown files, comments) — not a vulnerability
- Regex injection / regex DoS — not a vulnerability in this context

## Framework-Specific Precedents

These rules override general analysis when the specific framework is in use:

1. **React / Angular**: Safe from XSS by default. Only report XSS if code uses
   `dangerouslySetInnerHTML`, `bypassSecurityTrustHtml`, or equivalent escape hatches.
2. **Environment variables and CLI arguments**: Treated as trusted input. Attacks
   requiring control of env vars or CLI flags are invalid.
3. **GitHub Actions**: Only flag if untrusted input (e.g. `github.event.issue.title`)
   reaches a vulnerable step. Most workflow findings are not exploitable in practice.
4. **Client-side auth/permission checks**: Not a vulnerability. Server is responsible
   for authorization — client-side checks are UX, not security.
5. **UUIDs**: Assumed unguessable. Do not report missing UUID validation.
6. **Jupyter notebooks (.ipynb)**: Only flag if untrusted input can concretely trigger
   the vulnerability. Most notebook findings are not exploitable in practice.
7. **Shell scripts**: Command injection only valid if untrusted user input is proven
   to reach the vulnerable command. Shell scripts generally run with trusted input.
8. **Logging**: Logging non-PII data is not a vulnerability. Only report if secrets,
   passwords, or personally identifiable information (PII) are logged.
9. **URL logging**: Assumed safe — URLs in logs are not a vulnerability.
10. **AI/LLM prompts**: Including user-controlled content in AI system prompts is not
    a vulnerability (prompt injection is a separate concern outside security scan scope).
11. **Outdated libraries**: Managed separately by dependency scanning. Do not report
    known vulnerabilities in third-party libraries here.
12. **Subtle web vulnerabilities**: Do not report tabnabbing, XS-Leaks, prototype
    pollution, or open redirects unless extremely high confidence and demonstrated impact.

## Confidence Threshold

Every finding must include a confidence score (0-100).
See observation-schema.md for the confidence calibration table.
Findings in the Low band (< 30) are discarded unconditionally.
Findings in the Medium band (30-69) require clearly articulated exploitation conditions.
When in doubt, do not report. Prefer underreporting to overreporting.

## Output Format

For each finding, include:
- severity: HIGH | MEDIUM | LOW
- file: relative path from repo root
- start_line: integer
- end_line: integer (optional)
- title: short description (max 80 chars)
- description: detailed explanation with specific code reference
- confidence: 1-10

## Language

All output MUST be in English.
