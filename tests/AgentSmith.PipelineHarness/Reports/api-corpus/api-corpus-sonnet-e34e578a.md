# Api scan detection floor

> A TARGET THIS REPOSITORY SERVES ITSELF CANNOT GRADE THIS SCAN. Its weaknesses are authored, few and structural, so a green floor proves only that the api scan reaches the target, reads what it serves and emits findings at all. It is not a quality score and must never be quoted as one.

- model: `sonnet`
- api scan master: `e34e578a`
- target: `reference-target`
- generated: 2026-09-08T06:49:36.2139450+00:00

**Misses:** 0/4 (0 %) — declared weaknesses no delivered finding named.

**False alarms:** 0/3 (0 %) — sound endpoints a finding named anyway.

**Contributed nothing to this score:**
- Nuclei (stubbed in this tier — set AGENTSMITH_HARNESS_REAL_SCANNERS=1 with a docker daemon for dynamic evidence)
- Spectral (stubbed in this tier — set AGENTSMITH_HARNESS_REAL_SCANNERS=1 with a docker daemon for dynamic evidence)
- ZAP (stubbed in this tier — set AGENTSMITH_HARNESS_REAL_SCANNERS=1 with a docker daemon for dynamic evidence)

A score is not a complete measurement of a scan whose steps stayed silent.

## Endpoints
- [x] `GET /members/{id}` (missing-authorization, weak)
  - found [High]: GET /members/{id}: no security scheme declared — the endpoint is fully unauthenticated despite returning a Member record containing role and contactEmail
- [x] `GET /orders` (unscoped-identifier, weak)
  - found [High]: GET /orders: BOLA — memberId is a freely-supplied query parameter with no spec-stated binding to the bearer identity, allowing any authenticated member to enumerate another member's orders
- [x] `POST /invoices` (verbose-error, weak)
  - found [Medium]: POST /invoices: BOLA — orderId is freely supplied in the request body with no stated ownership check, allowing an authenticated member to create invoices against another member's orders
- [x] `PUT /members/{id}/role` (privilege-escalation, weak)
  - found [High]: PUT /members/{id}/role: BFLA + BOLA — any authenticated member (not just admins) can set the role of any arbitrary member by supplying an arbitrary {id}
- [x] `GET /health` (missing-authorization, sound)
- [x] `GET /orders/{id}` (unscoped-identifier, sound)
- [x] `POST /tokens/introspect` (credential-exposure, sound)
