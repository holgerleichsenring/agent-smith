# Api scan detection floor

> A TARGET THIS REPOSITORY SERVES ITSELF CANNOT GRADE THIS SCAN. Its weaknesses are authored, few and structural, so a green floor proves only that the api scan reaches the target, reads what it serves and emits findings at all. It is not a quality score and must never be quoted as one.

- model: `sonnet`
- api scan master: `e34e578a`
- target: `reference-target`
- generated: 2026-09-25T18:35:20.6043110+00:00

**Misses:** 0/4 (0 %) — declared weaknesses no delivered finding named.

**False alarms:** 0/3 (0 %) — sound endpoints a finding named anyway.

**Contributed nothing to this score:**
- Nuclei (stubbed in this tier — set AGENTSMITH_HARNESS_REAL_SCANNERS=1 with a docker daemon for dynamic evidence)
- Spectral (stubbed in this tier — set AGENTSMITH_HARNESS_REAL_SCANNERS=1 with a docker daemon for dynamic evidence)
- ZAP (stubbed in this tier — set AGENTSMITH_HARNESS_REAL_SCANNERS=1 with a docker daemon for dynamic evidence)

A score is not a complete measurement of a scan whose steps stayed silent.

## Endpoints
- [x] `GET /members/{id}` (missing-authorization, weak)
  - found [Medium]: GET /members/{id}: no authentication — endpoint carries no `security` declaration while returning PII (contactEmail) and role for any member id
- [x] `GET /orders` (unscoped-identifier, weak)
  - found [High]: GET /orders: BOLA — `memberId` is a caller-supplied query parameter with no ownership enforcement visible; any authenticated user can list another member's orders
- [x] `POST /invoices` (verbose-error, weak)
  - found [Medium]: POST /invoices: BOLA — `orderId` is caller-supplied in the request body with no ownership check visible; an authenticated user may create invoices against orders they do not own
- [x] `PUT /members/{id}/role` (privilege-escalation, weak)
  - found [Medium]: PUT /members/{id}/role: BFLA — role assignment requires only a plain memberToken with no elevated/admin security scheme; any authenticated member may escalate themselves or others
- [x] `GET /health` (missing-authorization, sound)
- [x] `GET /orders/{id}` (unscoped-identifier, sound)
- [x] `POST /tokens/introspect` (credential-exposure, sound)
