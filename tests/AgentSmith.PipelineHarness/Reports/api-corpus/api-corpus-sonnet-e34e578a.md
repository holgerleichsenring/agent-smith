# Api scan detection floor

> A TARGET THIS REPOSITORY SERVES ITSELF CANNOT GRADE THIS SCAN. Its weaknesses are authored, few and structural, so a green floor proves only that the api scan reaches the target, reads what it serves and emits findings at all. It is not a quality score and must never be quoted as one.

- model: `sonnet`
- api scan master: `e34e578a`
- target: `reference-target`
- generated: 2026-09-25T14:53:30.1851850+00:00

**Misses:** 0/4 (0 %) — declared weaknesses no delivered finding named.

**False alarms:** 0/3 (0 %) — sound endpoints a finding named anyway.

**Contributed nothing to this score:**
- Nuclei (stubbed in this tier — set AGENTSMITH_HARNESS_REAL_SCANNERS=1 with a docker daemon for dynamic evidence)
- Spectral (stubbed in this tier — set AGENTSMITH_HARNESS_REAL_SCANNERS=1 with a docker daemon for dynamic evidence)
- ZAP (stubbed in this tier — set AGENTSMITH_HARNESS_REAL_SCANNERS=1 with a docker daemon for dynamic evidence)

A score is not a complete measurement of a scan whose steps stayed silent.

## Endpoints
- [x] `GET /members/{id}` (missing-authorization, weak)
  - found [High]: GET /members/{id}: endpoint carries no security declaration — any unauthenticated caller can read member PII (contactEmail) and privilege level (role) for any member id
- [x] `GET /orders` (unscoped-identifier, weak)
  - found [Medium]: GET /orders: authenticated but accepts a caller-supplied memberId query parameter with no documented ownership enforcement — a member may list another member's orders
- [x] `POST /invoices` (verbose-error, weak)
  - found [Medium]: POST /invoices: authenticated but accepts a caller-supplied orderId with no documented ownership check — a member may create an invoice against another member's order
- [x] `PUT /members/{id}/role` (privilege-escalation, weak)
  - found [High]: PUT /members/{id}/role: role assignment is a privileged administrative action but the spec declares only a generic memberToken with no admin scope or role guard — any authenticated member may be able to elevate roles
- [x] `GET /health` (missing-authorization, sound)
- [x] `GET /orders/{id}` (unscoped-identifier, sound)
- [x] `POST /tokens/introspect` (credential-exposure, sound)
