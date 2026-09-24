# Api scan detection floor

> A TARGET THIS REPOSITORY SERVES ITSELF CANNOT GRADE THIS SCAN. Its weaknesses are authored, few and structural, so a green floor proves only that the api scan reaches the target, reads what it serves and emits findings at all. It is not a quality score and must never be quoted as one.

- model: `sonnet`
- api scan master: `e34e578a`
- target: `reference-target`
- generated: 2026-09-23T22:54:43.3097710+00:00

**Misses:** 0/4 (0 %) — declared weaknesses no delivered finding named.

**False alarms:** 0/3 (0 %) — sound endpoints a finding named anyway.

**Contributed nothing to this score:**
- Nuclei (stubbed in this tier — set AGENTSMITH_HARNESS_REAL_SCANNERS=1 with a docker daemon for dynamic evidence)
- Spectral (stubbed in this tier — set AGENTSMITH_HARNESS_REAL_SCANNERS=1 with a docker daemon for dynamic evidence)
- ZAP (stubbed in this tier — set AGENTSMITH_HARNESS_REAL_SCANNERS=1 with a docker daemon for dynamic evidence)

A score is not a complete measurement of a scan whose steps stayed silent.

## Endpoints
- [x] `GET /members/{id}` (missing-authorization, weak)
  - found [Medium]: GET /members/{id}: no authentication required — endpoint returns Member object including sensitive fields role and contactEmail without any bearer token
- [x] `GET /orders` (unscoped-identifier, weak)
  - found [Medium]: GET /orders: BOLA — client-supplied memberId query parameter allows an authenticated caller to list any member's orders
- [x] `POST /invoices` (verbose-error, weak)
  - found [Medium]: POST /invoices: potential BOLA — orderId is caller-supplied with no visible ownership check, allowing invoice creation against another member's order
- [x] `PUT /members/{id}/role` (privilege-escalation, weak)
  - found [Low]: PUT /members/{id}/role: broken function-level authorization — any authenticated member can set any member's role, including escalating to admin
- [x] `GET /health` (missing-authorization, sound)
- [x] `GET /orders/{id}` (unscoped-identifier, sound)
- [x] `POST /tokens/introspect` (credential-exposure, sound)
