# Api scan detection floor

> A TARGET THIS REPOSITORY SERVES ITSELF CANNOT GRADE THIS SCAN. Its weaknesses are authored, few and structural, so a green floor proves only that the api scan reaches the target, reads what it serves and emits findings at all. It is not a quality score and must never be quoted as one.

- model: `sonnet`
- api scan master: `e34e578a`
- target: `reference-target`
- generated: 2026-09-28T13:49:41.1011730+00:00

**Misses:** 0/4 (0 %) — declared weaknesses no delivered finding named.

**False alarms:** 0/3 (0 %) — sound endpoints a finding named anyway.

**Contributed nothing to this score:**
- Nuclei (stubbed in this tier — set AGENTSMITH_HARNESS_REAL_SCANNERS=1 with a docker daemon for dynamic evidence)
- Spectral (stubbed in this tier — set AGENTSMITH_HARNESS_REAL_SCANNERS=1 with a docker daemon for dynamic evidence)
- ZAP (stubbed in this tier — set AGENTSMITH_HARNESS_REAL_SCANNERS=1 with a docker daemon for dynamic evidence)

A score is not a complete measurement of a scan whose steps stayed silent.

## Endpoints
- [x] `GET /members/{id}` (missing-authorization, weak)
  - found [High]: GET /members/{id}: no `security` requirement is declared for this operation (every other data-bearing endpoint explicitly requires memberToken), so the member record — including role and contactEmail — appears retrievable without authentication
- [x] `GET /orders` (unscoped-identifier, weak)
  - found [Medium]: GET /orders: `memberId` is taken as a caller-supplied query parameter rather than derived from the authenticated bearer, so any authenticated member could list another member's orders (id, totalCents) by supplying a different memberId
- [x] `POST /invoices` (verbose-error, weak)
  - found [Medium]: POST /invoices: `orderId` is accepted from the request body with no described ownership check tying it to the caller's own orders, allowing a member to potentially create an invoice against another member's order
- [x] `PUT /members/{id}/role` (privilege-escalation, weak)
  - found [Medium]: PUT /members/{id}/role: any authenticated member token can set the `role` of an arbitrary member id (including their own) with no visible restriction to admin callers or to self-service-safe values
- [x] `GET /health` (missing-authorization, sound)
- [x] `GET /orders/{id}` (unscoped-identifier, sound)
- [x] `POST /tokens/introspect` (credential-exposure, sound)
