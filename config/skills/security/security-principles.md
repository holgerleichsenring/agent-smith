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
- SSRF where only the path is user-controlled (not host)
- Missing security headers without demonstrated exploit path
- Informational findings without actionable remediation

## Confidence Threshold

Every finding must include a confidence score (1-10).
Findings with confidence < 8 are discarded by the false-positive-filter.
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
