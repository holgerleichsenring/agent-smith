# Api scan detection floor

> A TARGET THIS REPOSITORY SERVES ITSELF CANNOT GRADE THIS SCAN. Its weaknesses are authored, few and structural, so a green floor proves only that the api scan reaches the target, reads what it serves and emits findings at all. It is not a quality score and must never be quoted as one.

- model: `sonnet`
- api scan master: `e34e578a`
- target: `reference-target`
- generated: 2026-09-28T12:45:49.8825530+00:00

**Misses:** 0/4 (0 %) — declared weaknesses no delivered finding named.

**False alarms:** 0/3 (0 %) — sound endpoints a finding named anyway.

**Contributed nothing to this score:**
- Nuclei (stubbed in this tier — set AGENTSMITH_HARNESS_REAL_SCANNERS=1 with a docker daemon for dynamic evidence)
- Spectral (stubbed in this tier — set AGENTSMITH_HARNESS_REAL_SCANNERS=1 with a docker daemon for dynamic evidence)
- ZAP (stubbed in this tier — set AGENTSMITH_HARNESS_REAL_SCANNERS=1 with a docker daemon for dynamic evidence)

A score is not a complete measurement of a scan whose steps stayed silent.

## Endpoints
- [x] `GET /members/{id}` (missing-authorization, weak)
  - found [High]: GET /members/{id}: unlike every other operation in this spec, this endpoint declares no security requirement, so any unauthenticated caller can read a member's displayName, role, and contactEmail by guessing/iterating ids
- [x] `GET /orders` (unscoped-identifier, weak)
  - found [High]: GET /orders: memberId is taken from a caller-supplied query parameter with no stated binding to the authenticated bearer's own identity, letting any authenticated member list any other member's orders (BOLA/IDOR) by varying memberId
- [x] `POST /invoices` (verbose-error, weak)
  - found [Medium]: POST /invoices: orderId is accepted in the request body with no stated ownership check, potentially allowing an authenticated member to create an invoice against another member's order
- [x] `PUT /members/{id}/role` (privilege-escalation, weak)
  - found [High]: PUT /members/{id}/role: any authenticated bearer can set the role of an arbitrary member id (including their own) to any string value with no visible ownership or privilege check — broken function-level authorization plus unrestricted mass-assignment of the role property
- [x] `GET /health` (missing-authorization, sound)
- [x] `GET /orders/{id}` (unscoped-identifier, sound)
- [x] `POST /tokens/introspect` (credential-exposure, sound)
