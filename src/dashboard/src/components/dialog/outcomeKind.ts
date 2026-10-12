// 2026-10-12-967ae: the word a person reads for an outcome kind. The server serves a filed spec's
// kind as "phase" until 2026-10-12-967ab (acceptedNames reads the later "spec" back as "phase"),
// so every place that SHOWS a kind says spec while every comparison keeps the served value.

/** The kind as the page says it: "spec" for the served "phase", any other kind unchanged. */
export function outcomeKindWord(kind: string): string {
  return kind === "phase" ? "spec" : kind;
}
