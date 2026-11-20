import { RUN_END_KINDS, type TicketAttempt } from "@/types/tickets";

// 2026-10-16-86e6b: how an attempt ended, as a mark — its status, a hand-back, an infrastructure end, a superseded park.
// 2026-11-17-8d1cb: out of the attempt card, with the end-kind words the state block's chips carried (7ed3b), so every
// attempt — not only the newest — says how and why it ended wherever it is listed.

const PILLS: Record<string, [string, string]> = {
  running: ["amber", "running"], queued: ["amber", "queued"], waiting_for_input: ["person", "waiting for an answer"],
  success: ["ok", "succeeded"], shortfall: ["amber", "shortfall"], failed: ["bad", "failed"], error: ["bad", "failed"],
  cancelled: ["q", "cancelled"],
};

const END_KIND_WORDS: Record<string, string> = {
  [RUN_END_KINDS.imageUntrusted]: "image not trusted",
  [RUN_END_KINDS.preflight]: "preflight failed",
  [RUN_END_KINDS.bootstrapMissing]: "bootstrap missing",
  [RUN_END_KINDS.criteriaUnmet]: "criterion not accepted",
  [RUN_END_KINDS.superseded]: "superseded",
  [RUN_END_KINDS.readinessUnknown]: "readiness unknown",
  [RUN_END_KINDS.seriesPaused]: "held while the series is paused", // 2026-11-19-1b72a
};

/** [class, words]: ok, amber, person, bad or q (quiet), and the outcome in words. */
export function outcomePill(attempt: TicketAttempt): [string, string] {
  // 2026-10-16-86e6b: a park a newer attempt started past is closed as superseded, whatever it waited on.
  if (attempt.endKind === RUN_END_KINDS.superseded) return ["q", "superseded"];
  // 2026-11-19-1b72a: a queued start its paused series held is no failure — quiet, and the resume starts it again.
  if (attempt.endKind === RUN_END_KINDS.seriesPaused) return ["q", END_KIND_WORDS[RUN_END_KINDS.seriesPaused]];
  if (attempt.handedBack) return ["person", "handed back"];
  if (attempt.endedOnInfrastructure) return ["bad", "ended on infrastructure"];
  return PILLS[attempt.status] ?? ["q", attempt.status.replace(/_/g, " ")];
}

/** "failed · image not trusted": the outcome, then the end kind only when it adds something the outcome does not say. */
export function attemptWords(attempt: TicketAttempt): string {
  const pill = outcomePill(attempt)[1];
  const kind = attempt.endKind ? END_KIND_WORDS[attempt.endKind] : undefined;
  return kind && kind !== pill ? `${pill} · ${kind}` : pill;
}

export type OutcomeTone = "ok" | "run" | "person" | "bad" | "quiet";

const TONES: Record<string, OutcomeTone> = { ok: "ok", amber: "run", person: "person", bad: "bad", q: "quiet" };

/** The colour an attempt's dot is drawn in: green passed, amber working, blue a person, red stopped, grey otherwise. */
export function outcomeTone(attempt: TicketAttempt): OutcomeTone {
  return TONES[outcomePill(attempt)[0]] ?? "quiet";
}
