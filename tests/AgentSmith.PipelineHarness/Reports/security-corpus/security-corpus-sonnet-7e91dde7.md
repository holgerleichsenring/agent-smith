# Security scan detection floor

> A PUBLIC CORPUS CANNOT GRADE THIS SCAN. Every well-known weakness is in the training data and a planted defect is formulaic in a way a real one is not, so a green floor proves only that the scan is wired, reaches the code and emits findings at all. It is not a quality score and must never be quoted as one.

- model: `sonnet`
- scan master: `7e91dde7`
- generated: 2026-09-28T12:49:12.7952770+00:00

**Misses:** 0/5 (0 %) — declared weaknesses no delivered finding named.

**False alarms:** 1/5 (20 %) — sound files a finding named anyway.

Cited line matched on 3 of 5 detections — a citation sub-metric, not a gate.

**Contributed nothing to this score:** DependencyAudit (did not run), GitHistoryScan (found nothing) — a score is not a complete measurement of a scan whose steps stayed silent.

## reference-service
- [x] src/orders/orderLookup.ts (sql-injection, flawed)
  - found [High]: default/src/orders/orderLookup.ts:7: findOrder builds SQL by concatenating req.params.id directly into the query string (SELECT ... WHERE id = '" + orderId + "'). (on the declared line)
- [x] src/admin/memberAdmin.ts (missing-authorization, flawed)
  - found [Critical]: default/src/admin/memberAdmin.ts:16: removeMember calls members.remove(req.params.id) without calling requireAdmin, unlike listMembers and suspendMember in the same file which both gate on it first.
- [x] src/files/attachmentDownload.ts (path-traversal, flawed)
  - found [High]: default/src/files/attachmentDownload.ts:9: downloadAttachment builds target = join(STORAGE_ROOT, name) from an unvalidated req.query.name and reads it, with no resolve()+containment check. (on the declared line)
- [x] src/auth/loginRedirect.ts (open-redirect, flawed)
  - found [Medium]: default/src/auth/loginRedirect.ts:6-7: finishLogin redirects to String(req.query.next) with no allowlist check, unlike returnToApp in returnTarget.ts which validates next against a fixed RETURN_TARGETS set.
- [x] src/jobs/jobPayload.ts (unsafe-deserialization, flawed)
  - found [High]: default/src/jobs/jobPayload.ts:7: readPayload rehydrates job.payloadText via eval("(" + raw + ")") instead of JSON.parse, executing arbitrary JavaScript found in the payload text. (on the declared line)
- [FALSE ALARM] src/reports/reportLookup.ts (sql-injection, clean)
  - found [Info]: default/src/reports/reportLookup.ts:15: template-literal SQL interpolates table/orderBy, but both are constrained to the hardcoded REPORTABLE allowlist (validated on line 12), not user input — scanner false positive. (on the declared line)
- [x] src/files/exportPath.ts (path-traversal, clean)
- [x] src/auth/returnTarget.ts (open-redirect, clean)
- [x] .agentsmith/contexts/default/context.yaml (project-metadata, clean)
- [x] .agentsmith/contexts/default/principles.md (project-metadata, clean)
