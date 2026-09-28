# Security scan detection floor

> A PUBLIC CORPUS CANNOT GRADE THIS SCAN. Every well-known weakness is in the training data and a planted defect is formulaic in a way a real one is not, so a green floor proves only that the scan is wired, reaches the code and emits findings at all. It is not a quality score and must never be quoted as one.

- model: `sonnet`
- scan master: `7e91dde7`
- generated: 2026-09-28T12:53:31.9873330+00:00

**Misses:** 0/5 (0 %) — declared weaknesses no delivered finding named.

**False alarms:** 1/5 (20 %) — sound files a finding named anyway.

Cited line matched on 2 of 5 detections — a citation sub-metric, not a gate.

**Contributed nothing to this score:** DependencyAudit (did not run), GitHistoryScan (found nothing) — a score is not a complete measurement of a scan whose steps stayed silent.

## reference-service
- [x] src/orders/orderLookup.ts (sql-injection, flawed)
  - found [High]: default/src/orders/orderLookup.ts:7: findOrder() builds SQL by string-concatenating req.params.id into the query text, unlike the parameterized countOrders() two lines below it (on the declared line)
- [x] src/admin/memberAdmin.ts (missing-authorization, flawed)
  - found [Critical]: default/src/admin/memberAdmin.ts:16-18: removeMember() calls members.remove(req.params.id) with no requireAdmin(req) check, unlike listMembers() and suspendMember() in the same file which both call requireAdmin() first
- [x] src/files/attachmentDownload.ts (path-traversal, flawed)
  - found [High]: default/src/files/attachmentDownload.ts:8-10: downloadAttachment() builds the file path with join(STORAGE_ROOT, name) from req.query.name but never resolves/contains it, unlike resolveExport() in the adjacent exportPath.ts
- [x] src/auth/loginRedirect.ts (open-redirect, flawed)
  - found [High]: default/src/auth/loginRedirect.ts:6-7: finishLogin() redirects to String(req.query.next) with no allowlist check, unlike safeReturnTarget()/RETURN_TARGETS in the adjacent src/auth/returnTarget.ts
- [x] src/jobs/jobPayload.ts (unsafe-deserialization, flawed)
  - found [Critical]: default/src/jobs/jobPayload.ts:7: readPayload() rehydrates job.payloadText via eval("(" + raw + ")") instead of JSON.parse, executing the payload text as JavaScript (on the declared line)
- [FALSE ALARM] src/reports/reportLookup.ts (sql-injection, clean)
  - found [Low]: default/src/reports/reportLookup.ts:12: `kind in REPORTABLE` uses the `in` operator, which also matches inherited Object.prototype keys ('__proto__', 'constructor', 'toString', etc.), letting those values pass the intended whitelist and reach the interpolated SQL table/ORDER BY clause (on the declared line)
- [x] src/files/exportPath.ts (path-traversal, clean)
- [x] src/auth/returnTarget.ts (open-redirect, clean)
- [x] .agentsmith/contexts/default/context.yaml (project-metadata, clean)
- [x] .agentsmith/contexts/default/principles.md (project-metadata, clean)
