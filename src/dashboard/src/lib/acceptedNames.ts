// 2026-10-12-967aa/967ab: served JSON in the phase vocabulary, read as the spec vocabulary this
// dashboard renders. The server writes `specId`, `specs`, `spec` and the kind "spec" since 967ab;
// during a rolling deploy this bundle may be served by a 967aa replica, and a stored trail or
// record may still carry the old names, so every body and hub payload passes through here once,
// at the seam where it arrives. 967ah removes it.
//
// ADDITIVE for property names: the declared name is filled from the accepted one and nothing
// is removed, so a shape that carries the old word for another meaning (an LLM call's `phase`,
// the execution stage) keeps it. Only the outcome kind is rewritten, and only the one value
// "phase", which nothing else serves.

const ACCEPTED_PROPERTIES: ReadonlyArray<readonly [accepted: string, declared: string]> = [
  ["phaseId", "specId"],
  ["phases", "specs"],
  ["phase", "spec"],
  ["executedPhaseIds", "executedSpecIds"],
];

const ACCEPTED_SPEC_KIND = "phase";
const DECLARED_SPEC_KIND = "spec";

/** The value with every accepted phase name readable under its spec name. Mutates in place. */
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
