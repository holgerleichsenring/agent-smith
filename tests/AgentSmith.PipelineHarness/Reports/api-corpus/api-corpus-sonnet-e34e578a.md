# Api scan detection floor

> A TARGET THIS REPOSITORY SERVES ITSELF CANNOT GRADE THIS SCAN. Its weaknesses are authored, few and structural, so a green floor proves only that the api scan reaches the target, reads what it serves and emits findings at all. It is not a quality score and must never be quoted as one.

- model: `sonnet`
- api scan master: `e34e578a`
- target: `reference-target`
- generated: 2026-09-02T05:20:26.8230470+00:00

**Misses:** 0/4 (0 %) — declared weaknesses no delivered finding named.

**False alarms:** 0/3 (0 %) — sound endpoints a finding named anyway.

**Contributed nothing to this score:**
- Nuclei (stubbed in this tier — set AGENTSMITH_HARNESS_REAL_SCANNERS=1 with a docker daemon for dynamic evidence)
- Spectral (stubbed in this tier — set AGENTSMITH_HARNESS_REAL_SCANNERS=1 with a docker daemon for dynamic evidence)
- ZAP (stubbed in this tier — set AGENTSMITH_HARNESS_REAL_SCANNERS=1 with a docker daemon for dynamic evidence)

A score is not a complete measurement of a scan whose steps stayed silent.

## Endpoints
- [x] `GET /members/{id}` (missing-authorization, weak)
  - found [High]: GET /members/{id}: no security scheme declared — endpoint is unauthenticated and returns Member PII including contactEmail and role
- [x] `GET /orders` (unscoped-identifier, weak)
  - found [Medium]: GET /orders: caller-supplied memberId query parameter — if not verified against the token subject, any member can enumerate another member's orders (BOLA)
- [x] `POST /invoices` (verbose-error, weak)
  - found [Medium]: POST /invoices: caller-supplied orderId with no visible ownership check — authenticated member may create invoices against orders they do not own
- [x] `PUT /members/{id}/role` (privilege-escalation, weak)
  - found [High]: PUT /members/{id}/role: no privilege tier declared — any authenticated member bearer can set any member's role, including privilege escalation
- [x] `GET /health` (missing-authorization, sound)
- [x] `GET /orders/{id}` (unscoped-identifier, sound)
- [x] `POST /tokens/introspect` (credential-exposure, sound)
