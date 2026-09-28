# Api scan detection floor

> A TARGET THIS REPOSITORY SERVES ITSELF CANNOT GRADE THIS SCAN. Its weaknesses are authored, few and structural, so a green floor proves only that the api scan reaches the target, reads what it serves and emits findings at all. It is not a quality score and must never be quoted as one.

- model: `sonnet`
- api scan master: `e34e578a`
- target: `reference-target`
- generated: 2026-09-28T12:57:47.6363260+00:00

**Misses:** 0/4 (0 %) — declared weaknesses no delivered finding named.

**False alarms:** 0/3 (0 %) — sound endpoints a finding named anyway.

**Contributed nothing to this score:**
- Nuclei (stubbed in this tier — set AGENTSMITH_HARNESS_REAL_SCANNERS=1 with a docker daemon for dynamic evidence)
- Spectral (stubbed in this tier — set AGENTSMITH_HARNESS_REAL_SCANNERS=1 with a docker daemon for dynamic evidence)
- ZAP (stubbed in this tier — set AGENTSMITH_HARNESS_REAL_SCANNERS=1 with a docker daemon for dynamic evidence)

A score is not a complete measurement of a scan whose steps stayed silent.

## Endpoints
- [x] `GET /members/{id}` (missing-authorization, weak)
  - found [High]: GET /members/{id}: no security requirement is declared for this operation (unlike every other data-bearing endpoint in the spec), yet it returns role and contactEmail — full member PII readable with no bearer token at all
- [x] `GET /orders` (unscoped-identifier, weak)
  - found [Medium]: GET /orders?memberId={id}: listOrders takes memberId as a client-supplied query parameter with no stated ownership check, in contrast to GET /orders/{id} which the spec explicitly documents as 'scoped to the bearer's own member id'
- [x] `POST /invoices` (verbose-error, weak)
  - found [Low]: POST /invoices: createInvoice accepts an orderId in the request body with no described check that the order belongs to the caller, allowing a member to generate an invoice against another member's order
- [x] `PUT /members/{id}/role` (privilege-escalation, weak)
  - found [Medium]: PUT /members/{id}/role: setMemberRole only requires a valid memberToken bearer with no described privilege check, so any authenticated member could set any member's role field, including their own, to an elevated value
- [x] `GET /health` (missing-authorization, sound)
- [x] `GET /orders/{id}` (unscoped-identifier, sound)
- [x] `POST /tokens/introspect` (credential-exposure, sound)
