// 2026-10-12-967ae: the word a person reads for an outcome kind. 2026-10-12-967ab: the server
// serves a filed spec's kind as "spec"; a replica still on 967aa or a record stored before may
// serve "phase" (acceptedNames reads it forward), and that is read as spec too until 967ah.

/** The kind as the page says it: "spec" for a spec under either served kind, any other kind unchanged. */
export function outcomeKindWord(kind: string): string {
  return kind === "phase" ? "spec" : kind;
}
