# Api scan detection floor

> A TARGET THIS REPOSITORY SERVES ITSELF CANNOT GRADE THIS SCAN. Its weaknesses are authored, few and structural, so a green floor proves only that the api scan reaches the target, reads what it serves and emits findings at all. It is not a quality score and must never be quoted as one.

- model: `sonnet`
- api scan master: `e34e578a`
- target: `reference-target`
- generated: 2026-09-01T21:53:17.0950560+00:00

**Misses:** 0/4 (0 %) — declared weaknesses no delivered finding named.

**False alarms:** 0/3 (0 %) — sound endpoints a finding named anyway.

**Contributed nothing to this score:**
- Nuclei (stubbed in this tier — set AGENTSMITH_HARNESS_REAL_SCANNERS=1 with a docker daemon for dynamic evidence)
- Spectral (stubbed in this tier — set AGENTSMITH_HARNESS_REAL_SCANNERS=1 with a docker daemon for dynamic evidence)
- ZAP (stubbed in this tier — set AGENTSMITH_HARNESS_REAL_SCANNERS=1 with a docker daemon for dynamic evidence)

A score is not a complete measurement of a scan whose steps stayed silent.

## Endpoints
- [x] `GET /members/{id}` (missing-authorization, weak)
  - found [High]: GET /members/{id}: no authentication required — endpoint carries no `security` declaration while returning sensitive Member fields (role, contactEmail) to unauthenticated callers
- [x] `GET /orders` (unscoped-identifier, weak)
  - found [High]: GET /orders: BOLA — caller-supplied `memberId` query parameter is not bound to the token identity per spec, allowing any authenticated member to enumerate any other member's orders
- [x] `POST /invoices` (verbose-error, weak)
  - found [Medium]: POST /invoices: no ownership check on `orderId` — authenticated user can create an invoice against an order they do not own
- [x] `PUT /members/{id}/role` (privilege-escalation, weak)
  - found [Low]: PUT /members/{id}/role: broken function-level authorization — any bearer token holder can set any member's role; no elevated/admin privilege requirement declared
- [x] `GET /health` (missing-authorization, sound)
- [x] `GET /orders/{id}` (unscoped-identifier, sound)
- [x] `POST /tokens/introspect` (credential-exposure, sound)
