# Api scan detection floor

> A TARGET THIS REPOSITORY SERVES ITSELF CANNOT GRADE THIS SCAN. Its weaknesses are authored, few and structural, so a green floor proves only that the api scan reaches the target, reads what it serves and emits findings at all. It is not a quality score and must never be quoted as one.

- model: `sonnet`
- api scan master: `e34e578a`
- target: `reference-target`
- generated: 2026-09-01T22:04:35.1879810+00:00

**Misses:** 0/4 (0 %) — declared weaknesses no delivered finding named.

**False alarms:** 0/3 (0 %) — sound endpoints a finding named anyway.

**Contributed nothing to this score:**
- Nuclei (stubbed in this tier — set AGENTSMITH_HARNESS_REAL_SCANNERS=1 with a docker daemon for dynamic evidence)
- Spectral (stubbed in this tier — set AGENTSMITH_HARNESS_REAL_SCANNERS=1 with a docker daemon for dynamic evidence)
- ZAP (stubbed in this tier — set AGENTSMITH_HARNESS_REAL_SCANNERS=1 with a docker daemon for dynamic evidence)

A score is not a complete measurement of a scan whose steps stayed silent.

## Endpoints
- [x] `GET /members/{id}` (missing-authorization, weak)
  - found [Medium]: GET /members/{id}: No authentication declared — endpoint returns Member schema including contactEmail (PII) and role field to any caller without a bearer token
- [x] `GET /orders` (unscoped-identifier, weak)
  - found [Medium]: GET /orders: IDOR — caller-supplied memberId query parameter with no spec-level ownership enforcement; any authenticated member can list another member's orders
- [x] `POST /invoices` (verbose-error, weak)
  - found [Medium]: POST /invoices: No spec-level ownership check — caller-supplied orderId is not constrained to orders belonging to the authenticated member
- [x] `PUT /members/{id}/role` (privilege-escalation, weak)
  - found [High]: PUT /members/{id}/role: BFLA — role assignment operation is gated only by a standard memberToken with no elevated or admin-tier security scheme declared
- [x] `GET /health` (missing-authorization, sound)
- [x] `GET /orders/{id}` (unscoped-identifier, sound)
- [x] `POST /tokens/introspect` (credential-exposure, sound)
