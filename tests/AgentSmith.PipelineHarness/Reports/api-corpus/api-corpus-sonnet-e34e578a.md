# Api scan detection floor

> A TARGET THIS REPOSITORY SERVES ITSELF CANNOT GRADE THIS SCAN. Its weaknesses are authored, few and structural, so a green floor proves only that the api scan reaches the target, reads what it serves and emits findings at all. It is not a quality score and must never be quoted as one.

- model: `sonnet`
- api scan master: `e34e578a`
- target: `reference-target`
- generated: 2026-09-28T15:00:24.6221520+00:00

**Misses:** 0/4 (0 %) — declared weaknesses no delivered finding named.

**False alarms:** 0/3 (0 %) — sound endpoints a finding named anyway.

**Contributed nothing to this score:**
- Nuclei (stubbed in this tier — set AGENTSMITH_HARNESS_REAL_SCANNERS=1 with a docker daemon for dynamic evidence)
- Spectral (stubbed in this tier — set AGENTSMITH_HARNESS_REAL_SCANNERS=1 with a docker daemon for dynamic evidence)
- ZAP (stubbed in this tier — set AGENTSMITH_HARNESS_REAL_SCANNERS=1 with a docker daemon for dynamic evidence)

A score is not a complete measurement of a scan whose steps stayed silent.

## Endpoints
- [x] `GET /members/{id}` (missing-authorization, weak)
  - found [High]: GET /members/{id}: no security requirement is declared on this operation at all, while every other data-bearing operation in the spec (PUT /members/{id}/role, GET /orders, GET /orders/{id}, POST /invoices, POST /tokens/introspect) explicitly requires memberToken bearer auth. The response returns a full Member record including contactEmail and role with zero authentication.
- [x] `GET /orders` (unscoped-identifier, weak)
  - found [High]: GET /orders: memberId is a client-supplied required query parameter used to select whose orders are returned, with no indication it is cross-checked against the bearer's own identity — unlike GET /orders/{id}, whose summary explicitly states it is 'Scoped to the bearer's own member id' and returns 404 otherwise.
- [x] `POST /invoices` (verbose-error, weak)
  - found [Medium]: POST /invoices: orderId is accepted from the client with no documented ownership check against the caller's member id, and the only documented failure mode is a generic 500 — unlike GET /orders/{id}, which documents a distinct 404 specifically for 'no such order for this member'.
- [x] `PUT /members/{id}/role` (privilege-escalation, weak)
  - found [High]: PUT /members/{id}/role: any caller holding a valid memberToken appears able to set the role of an arbitrary member id (including their own) to an arbitrary string value, with no spec indication of an admin-only restriction or an allow-list of role values.
- [x] `GET /health` (missing-authorization, sound)
- [x] `GET /orders/{id}` (unscoped-identifier, sound)
- [x] `POST /tokens/introspect` (credential-exposure, sound)
