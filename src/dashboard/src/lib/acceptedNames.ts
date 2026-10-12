// 2026-10-12-967aa: served JSON in the spec vocabulary, read as the phase vocabulary this
// dashboard renders. A later server release writes `specId`, `specs`, `spec` and the kind
// "spec"; during a rolling deploy this bundle may be served by either release, so every body
// and hub payload passes through here once, at the seam where it arrives.
//
// ADDITIVE for property names: the declared name is filled from the accepted one and nothing
// is removed, because `specId` and `specs` are ALSO current names on other shapes (a run's
// spec row, a filed-work run) that must keep reading as they do. Only the outcome kind is
// rewritten, and only the one value "spec", which nothing else serves.

const ACCEPTED_PROPERTIES: ReadonlyArray<readonly [accepted: string, declared: string]> = [
  ["specId", "phaseId"],
  ["specs", "phases"],
  ["spec", "phase"],
  ["executedSpecIds", "executedPhaseIds"],
];

const ACCEPTED_SPEC_KIND = "spec";
const DECLARED_SPEC_KIND = "phase";

/** The value with every accepted spec name readable under its phase name. Mutates in place. */
export function acceptSpecNames<T>(value: T): T {
  visit(value);
  return value;
}

function visit(value: unknown): void {
  if (Array.isArray(value)) {
    for (const item of value) visit(item);
    return;
  }
  if (value === null || typeof value !== "object") return;
  const record = value as Record<string, unknown>;
  for (const [accepted, declared] of ACCEPTED_PROPERTIES) {
    if (accepted in record && !(declared in record)) record[declared] = record[accepted];
  }
  if (record.kind === ACCEPTED_SPEC_KIND) record.kind = DECLARED_SPEC_KIND;
  for (const child of Object.values(record)) visit(child);
}
