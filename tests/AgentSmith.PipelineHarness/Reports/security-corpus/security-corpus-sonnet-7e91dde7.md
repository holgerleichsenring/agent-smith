# Security scan detection floor

> A PUBLIC CORPUS CANNOT GRADE THIS SCAN. Every well-known weakness is in the training data and a planted defect is formulaic in a way a real one is not, so a green floor proves only that the scan is wired, reaches the code and emits findings at all. It is not a quality score and must never be quoted as one.

- model: `sonnet`
- scan master: `7e91dde7`
- generated: 2026-09-25T18:34:20.9394300+00:00

**Misses:** 0/5 (0 %) — declared weaknesses no delivered finding named.

**False alarms:** 1/5 (20 %) — sound files a finding named anyway.

Cited line matched on 4 of 5 detections — a citation sub-metric, not a gate.

**Contributed nothing to this score:** DependencyAudit (did not run), GitHistoryScan (found nothing) — a score is not a complete measurement of a scan whose steps stayed silent.

## reference-service
- [x] src/orders/orderLookup.ts (sql-injection, flawed)
  - found [Medium]: src/orders/orderLookup.ts:7: SQL built by string-concatenating the route parameter `id` — classic SQL injection (on the declared line)
- [x] src/admin/memberAdmin.ts (missing-authorization, flawed)
  - found [High]: src/admin/memberAdmin.ts:16: removeMember omits requireAdmin(req) — any caller can delete members without an admin role check
- [x] src/files/attachmentDownload.ts (path-traversal, flawed)
  - found [High]: src/files/attachmentDownload.ts:9: path.join used without resolve+containment check — path traversal escapes STORAGE_ROOT (on the declared line)
- [x] src/auth/loginRedirect.ts (open-redirect, flawed)
  - found [High]: src/auth/loginRedirect.ts:7: open redirect — res.redirect(next) with no allowlist check on the caller-supplied `next` query parameter (on the declared line)
- [x] src/jobs/jobPayload.ts (unsafe-deserialization, flawed)
  - found [Critical]: src/jobs/jobPayload.ts:7: eval() used to deserialize job payload — arbitrary code execution if a producer or queue storage is compromised (on the declared line)
- [FALSE ALARM] src/reports/reportLookup.ts (sql-injection, clean)
  - found [Medium]: src/reports/reportLookup.ts:15: scanner flag dismissed — template literal SQL is safe; table and orderBy derive from a const allowlist, not caller input (on the declared line)
- [x] src/files/exportPath.ts (path-traversal, clean)
- [x] src/auth/returnTarget.ts (open-redirect, clean)
- [x] .agentsmith/contexts/default/context.yaml (project-metadata, clean)
- [x] .agentsmith/contexts/default/principles.md (project-metadata, clean)
