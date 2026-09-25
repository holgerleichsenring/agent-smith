# Api scan detection floor

> A TARGET THIS REPOSITORY SERVES ITSELF CANNOT GRADE THIS SCAN. Its weaknesses are authored, few and structural, so a green floor proves only that the api scan reaches the target, reads what it serves and emits findings at all. It is not a quality score and must never be quoted as one.

- model: `sonnet`
- api scan master: `e34e578a`
- target: `reference-target`
- generated: 2026-09-25T18:15:54.6919820+00:00

**Misses:** 0/4 (0 %) — declared weaknesses no delivered finding named.

**False alarms:** 1/3 (33 %) — sound endpoints a finding named anyway.

**Contributed nothing to this score:**
- Nuclei (stubbed in this tier — set AGENTSMITH_HARNESS_REAL_SCANNERS=1 with a docker daemon for dynamic evidence)
- Spectral (stubbed in this tier — set AGENTSMITH_HARNESS_REAL_SCANNERS=1 with a docker daemon for dynamic evidence)
- ZAP (stubbed in this tier — set AGENTSMITH_HARNESS_REAL_SCANNERS=1 with a docker daemon for dynamic evidence)

A score is not a complete measurement of a scan whose steps stayed silent.

## Endpoints
- [x] `GET /members/{id}` (missing-authorization, weak)
  - found [Medium]: GET /members/{id}: endpoint carries no security scheme, returning Member object including sensitive fields 'role' and 'contactEmail' to unauthenticated callers.
- [x] `GET /orders` (unscoped-identifier, weak)
  - found [Medium]: GET /orders?memberId=: BOLA — caller-supplied 'memberId' query parameter is not constrained to the bearer's own identity in the spec, enabling cross-member order enumeration.
- [x] `POST /invoices` (verbose-error, weak)
  - found [Medium]: POST /invoices: caller-supplied 'orderId' body field has no ownership assertion in the spec — an authenticated member may create invoices against orders belonging to other members.
- [x] `PUT /members/{id}/role` (privilege-escalation, weak)
  - found [Low]: PUT /members/{id}/role: authenticated but no privilege check declared — any bearer token holder can set an arbitrary member's role, enabling privilege escalation.
- [x] `GET /health` (missing-authorization, sound)
- [FALSE ALARM] `GET /orders/{id}` (unscoped-identifier, sound)
  - found [Medium]: GET /orders/{id}: Ownership scoping is spec-declared but unverifiable — if implementation omits the bearer-to-member check, BOLA results.
- [x] `POST /tokens/introspect` (credential-exposure, sound)
