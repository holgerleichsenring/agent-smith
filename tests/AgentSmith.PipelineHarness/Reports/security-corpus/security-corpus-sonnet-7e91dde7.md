# Security scan detection floor

> A PUBLIC CORPUS CANNOT GRADE THIS SCAN. Every well-known weakness is in the training data and a planted defect is formulaic in a way a real one is not, so a green floor proves only that the scan is wired, reaches the code and emits findings at all. It is not a quality score and must never be quoted as one.

- model: `sonnet`
- scan master: `7e91dde7`
- generated: 2026-09-28T12:42:48.1728080+00:00

**Misses:** 0/5 (0 %) — declared weaknesses no delivered finding named.

**False alarms:** 0/5 (0 %) — sound files a finding named anyway.

Cited line matched on 3 of 5 detections — a citation sub-metric, not a gate.

**Contributed nothing to this score:** DependencyAudit (did not run), GitHistoryScan (found nothing) — a score is not a complete measurement of a scan whose steps stayed silent.

## reference-service
- [x] src/orders/orderLookup.ts (sql-injection, flawed)
  - found [Critical]: src/orders/orderLookup.ts:7: findOrder builds SQL by concatenating req.params.id directly into the query string, allowing SQL injection via the order id route parameter. (on the declared line)
- [x] src/admin/memberAdmin.ts (missing-authorization, flawed)
  - found [High]: src/admin/memberAdmin.ts:16-19: removeMember deletes a member without calling requireAdmin, unlike listMembers and suspendMember in the same file.
- [x] src/files/attachmentDownload.ts (path-traversal, flawed)
  - found [High]: src/files/attachmentDownload.ts:9-10: downloadAttachment joins STORAGE_ROOT with the unvalidated 'name' query parameter and reads it without resolving or containing the result, allowing path traversal. (on the declared line)
- [x] src/auth/loginRedirect.ts (open-redirect, flawed)
  - found [Medium]: src/auth/loginRedirect.ts:6-7: finishLogin redirects to the raw req.query.next value with no allowlist check, enabling an open redirect after login.
- [x] src/jobs/jobPayload.ts (unsafe-deserialization, flawed)
  - found [High]: src/jobs/jobPayload.ts:7: readPayload rehydrates a queued job's payload with eval("(" + raw + ")"), executing the stored payload text as JavaScript instead of parsing it as data. (on the declared line)
- [x] src/reports/reportLookup.ts (sql-injection, clean)
- [x] src/files/exportPath.ts (path-traversal, clean)
- [x] src/auth/returnTarget.ts (open-redirect, clean)
- [x] .agentsmith/contexts/default/context.yaml (project-metadata, clean)
- [x] .agentsmith/contexts/default/principles.md (project-metadata, clean)
