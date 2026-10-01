# Api scan detection floor

> A TARGET THIS REPOSITORY SERVES ITSELF CANNOT GRADE THIS SCAN. Its weaknesses are authored, few and structural, so a green floor proves only that the api scan reaches the target, reads what it serves and emits findings at all. It is not a quality score and must never be quoted as one.

- model: `sonnet`
- api scan master: `e34e578a`
- target: `reference-target`
- generated: 2026-10-01T18:23:06.7563240+00:00

**Misses:** 0/4 (0 %) — declared weaknesses no delivered finding named.

**False alarms:** 0/3 (0 %) — sound endpoints a finding named anyway.

**Contributed nothing to this score:**
- Nuclei (stubbed in this tier — set AGENTSMITH_HARNESS_REAL_SCANNERS=1 with a docker daemon for dynamic evidence)
- Spectral (stubbed in this tier — set AGENTSMITH_HARNESS_REAL_SCANNERS=1 with a docker daemon for dynamic evidence)
- ZAP (stubbed in this tier — set AGENTSMITH_HARNESS_REAL_SCANNERS=1 with a docker daemon for dynamic evidence)

A score is not a complete measurement of a scan whose steps stayed silent.

## Endpoints
- [x] `GET /members/{id}` (missing-authorization, weak)
  - found [Medium]: GET /members/{id}: no security scheme is declared on this operation (unlike every other data-returning endpoint), so the handler appears reachable without a bearer token while still returning contactEmail and role — PII and privilege data exposed without authentication
- [x] `GET /orders` (unscoped-identifier, weak)
  - found [Medium]: GET /orders: memberId is taken as a caller-supplied query parameter rather than derived from the authenticated bearer, unlike GET /orders/{id} whose summary explicitly states it is 'scoped to the bearer's own member id' — the contrast suggests listOrders may not enforce that scoping, allowing enumeration of other members' orders
- [x] `POST /invoices` (verbose-error, weak)
  - found [Medium]: POST /invoices: orderId is accepted in the request body with no stated ownership verification against the bearer's member id, potentially allowing a member to create an invoice against another member's order
- [x] `PUT /members/{id}/role` (privilege-escalation, weak)
  - found [High]: PUT /members/{id}/role: any holder of a valid memberToken can set the role for an arbitrary {id} — no function-level check distinguishing self-service from admin-only callers is visible in the contract, enabling privilege escalation
- [x] `GET /health` (missing-authorization, sound)
- [x] `GET /orders/{id}` (unscoped-identifier, sound)
- [x] `POST /tokens/introspect` (credential-exposure, sound)
