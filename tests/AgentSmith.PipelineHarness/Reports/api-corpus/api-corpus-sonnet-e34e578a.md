# Api scan detection floor

> A TARGET THIS REPOSITORY SERVES ITSELF CANNOT GRADE THIS SCAN. Its weaknesses are authored, few and structural, so a green floor proves only that the api scan reaches the target, reads what it serves and emits findings at all. It is not a quality score and must never be quoted as one.

- model: `sonnet`
- api scan master: `e34e578a`
- target: `reference-target`
- generated: 2026-09-28T12:54:28.4390190+00:00

**Misses:** 0/4 (0 %) — declared weaknesses no delivered finding named.

**False alarms:** 0/3 (0 %) — sound endpoints a finding named anyway.

**Contributed nothing to this score:**
- Nuclei (stubbed in this tier — set AGENTSMITH_HARNESS_REAL_SCANNERS=1 with a docker daemon for dynamic evidence)
- Spectral (stubbed in this tier — set AGENTSMITH_HARNESS_REAL_SCANNERS=1 with a docker daemon for dynamic evidence)
- ZAP (stubbed in this tier — set AGENTSMITH_HARNESS_REAL_SCANNERS=1 with a docker daemon for dynamic evidence)

A score is not a complete measurement of a scan whose steps stayed silent.

## Endpoints
- [x] `GET /members/{id}` (missing-authorization, weak)
  - found [High]: GET /members/{id}: no security requirement is declared on the operation (and no global security default exists in the document), so the endpoint returns a member's displayName, role and contactEmail to anonymous callers while the sibling PUT /members/{id}/role requires memberToken
- [x] `GET /orders` (unscoped-identifier, weak)
  - found [Medium]: GET /orders: identity is taken from a client-supplied `memberId` query parameter rather than derived from the bearer token, so an authenticated member can pass another member's id to list that member's orders (order totals, order ids)
- [x] `POST /invoices` (verbose-error, weak)
  - found [Medium]: POST /invoices: accepts a client-supplied `orderId` with no documented check that the order belongs to the caller's member, allowing a member to create an invoice against another member's order
- [x] `PUT /members/{id}/role` (privilege-escalation, weak)
  - found [High]: PUT /members/{id}/role: any holder of a valid memberToken can set the role of any member id (including their own) to any string value, with no ownership or privilege check documented — broken function-level authorization enabling privilege escalation
- [x] `GET /health` (missing-authorization, sound)
- [x] `GET /orders/{id}` (unscoped-identifier, sound)
- [x] `POST /tokens/introspect` (credential-exposure, sound)
