# Security scan detection floor

> A PUBLIC CORPUS CANNOT GRADE THIS SCAN. Every well-known weakness is in the training data and a planted defect is formulaic in a way a real one is not, so a green floor proves only that the scan is wired, reaches the code and emits findings at all. It is not a quality score and must never be quoted as one.

- model: `sonnet`
- scan master: `7e91dde7`
- generated: 2026-09-27T15:41:07.2677760+00:00

**Misses:** 0/5 (0 %) — declared weaknesses no delivered finding named.

**False alarms:** 0/5 (0 %) — sound files a finding named anyway.

Cited line matched on 3 of 5 detections — a citation sub-metric, not a gate.

**Contributed nothing to this score:** DependencyAudit (did not run), GitHistoryScan (found nothing) — a score is not a complete measurement of a scan whose steps stayed silent.

## reference-service
- [x] src/orders/orderLookup.ts (sql-injection, flawed)
  - found [High]: src/orders/orderLookup.ts:7: SQL query built by string-concatenating req.params.id — SQL injection (on the declared line)
- [x] src/admin/memberAdmin.ts (missing-authorization, flawed)
  - found [High]: src/admin/memberAdmin.ts:16: removeMember omits the requireAdmin() check present on every other admin handler
- [x] src/files/attachmentDownload.ts (path-traversal, flawed)
  - found [High]: src/files/attachmentDownload.ts:9: path built with join() but not resolved/contained — path traversal (on the declared line)
- [x] src/auth/loginRedirect.ts (open-redirect, flawed)
  - found [Medium]: src/auth/loginRedirect.ts:6: open redirect — req.query.next is reflected into res.redirect() without validation
- [x] src/jobs/jobPayload.ts (unsafe-deserialization, flawed)
  - found [Critical]: src/jobs/jobPayload.ts:7: eval() called on untrusted job payload — arbitrary code execution (on the declared line)
- [x] src/reports/reportLookup.ts (sql-injection, clean)
- [x] src/files/exportPath.ts (path-traversal, clean)
- [x] src/auth/returnTarget.ts (open-redirect, clean)
- [x] .agentsmith/contexts/default/context.yaml (project-metadata, clean)
- [x] .agentsmith/contexts/default/principles.md (project-metadata, clean)
