# Api scan detection floor

> A TARGET THIS REPOSITORY SERVES ITSELF CANNOT GRADE THIS SCAN. Its weaknesses are authored, few and structural, so a green floor proves only that the api scan reaches the target, reads what it serves and emits findings at all. It is not a quality score and must never be quoted as one.

- model: `sonnet`
- api scan master: `e34e578a`
- target: `reference-target`
- generated: 2026-10-14T08:36:32.9696760+00:00

**Misses:** 0/4 (0 %) — declared weaknesses no delivered finding named.

**False alarms:** 0/3 (0 %) — sound endpoints a finding named anyway.

**Contributed nothing to this score:**
- Nuclei (stubbed in this tier — set AGENTSMITH_HARNESS_REAL_SCANNERS=1 with a docker daemon for dynamic evidence)
- Spectral (stubbed in this tier — set AGENTSMITH_HARNESS_REAL_SCANNERS=1 with a docker daemon for dynamic evidence)
- ZAP (stubbed in this tier — set AGENTSMITH_HARNESS_REAL_SCANNERS=1 with a docker daemon for dynamic evidence)

A score is not a complete measurement of a scan whose steps stayed silent.

## Endpoints
- [x] `GET /members/{id}` (missing-authorization, weak)
  - found [Medium]: GET /members/{id}: the operation declares no security requirement, yet it returns the Member object including role and contactEmail. Anyone can read any member's record by guessing or enumerating the id.
- [x] `GET /orders` (unscoped-identifier, weak)
  - found [Medium]: GET /orders: the target member comes from the client-supplied required query parameter `memberId`. If the server does not compare it with the bearer's own member id, any authenticated user can list another member's orders (BOLA/IDOR).
- [x] `POST /invoices` (verbose-error, weak)
  - found [Low]: POST /invoices: the body takes a client-supplied `orderId`. The spec does not show an ownership check, so a caller may be able to create invoices for orders that are not theirs. The documented 500 response also hints at unhandled errors, for example for an unknown orderId.
- [x] `PUT /members/{id}/role` (privilege-escalation, weak)
  - found [Medium]: PUT /members/{id}/role: any holder of a memberToken may be able to set any member's role, including their own. The spec shows no admin or role requirement, and the body takes a free-form string `role`. This is a function-level authorization and privilege-escalation risk.
- [x] `GET /health` (missing-authorization, sound)
- [x] `GET /orders/{id}` (unscoped-identifier, sound)
- [x] `POST /tokens/introspect` (credential-exposure, sound)
