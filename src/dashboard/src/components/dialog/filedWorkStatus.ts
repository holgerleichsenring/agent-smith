// 2026-09-17-042ef, found by looking at the rendered page: a phase's status arrived on screen as
// the column value the projection writes — "in_progress", "not_started" — set inline in the
// prose around it, so a state was indistinguishable from a sentence. The Projects page renders
// every fact about a project as a MARK; a state is a fact, and it gets one here too.
//
// The words are this module's, the tone is its second answer: a mark is neutral, warn or bad,
// and those three are the studio's whole alarm vocabulary (mock-parity.css:531-533). That is a
// different question from the Badge's four-tone one on the runs list, which has a green for a
// success a mark has no equivalent of — so the two are not one answer copied twice.

/** The modifier a mark carries: "" is the studio's neutral mark, warn and bad its two alarms. */
export type MarkTone = "" | "warn" | "bad";

export interface StatusWords {
  word: string;
  tone: MarkTone;
}

// RunPhaseProjection.StatusOf writes exactly these five and nothing else.
const PHASE_STATUS: Record<string, StatusWords> = {
  not_started: { word: "not started", tone: "" },
  in_progress: { word: "running", tone: "" },
  done: { word: "done", tone: "" },
  // 2026-09-17-0e79c: the two stopped states ask OPPOSITE things of the operator — a red build
  // is fixed in the code, a false premise by amending the spec here — so they never share a
  // tone any more than they share a word.
  failed: { word: "failed", tone: "bad" },
  handed_back: { word: "handed back", tone: "warn" },
};

/** What a phase's status column says, in words, and which mark carries it. */
export function phaseStatusWords(status: string): StatusWords {
  return PHASE_STATUS[status] ?? { word: status.replace(/_/g, " "), tone: "" };
}

// RunPhaseProjection.IsTerminal — the three states a phase does not leave again.
const TERMINAL = new Set(["done", "failed", "handed_back"]);

/** Whether the phase has stopped. A phase still running has not reached its review yet; one
 *  that has stopped and carries no review row is a gap in the evidence, not a not-yet. */
export function isPhaseTerminal(status: string): boolean {
  return TERMINAL.has(status);
}

// RunPullRequestView's documented vocabulary is "opened" | "no_changes" | "failed"
// (PullRequestContracts.cs). It reached the screen raw, underscore and all.
const PULL_REQUEST_STATUS: Record<string, StatusWords> = {
  opened: { word: "opened", tone: "" },
  no_changes: { word: "no changes", tone: "" },
  // A pull request that could not be opened is a thing that did not happen, and it read in the
  // same grey as one that did while a skipped review one panel over was amber.
  failed: { word: "could not be opened", tone: "bad" },
};

/** What a pull request's status column says, in words, and which mark carries it. */
export function pullRequestWords(status: string): StatusWords {
  return PULL_REQUEST_STATUS[status] ?? { word: status.replace(/_/g, " "), tone: "" };
}

/** Which mark a run's status column earns. The words come from runStatusWord. */
export function runStatusTone(status: string): MarkTone {
  const said = status.toLowerCase();
  if (said === "failed" || said === "error") return "bad";
  // A run waiting for capacity or for a person is not a failure; it is somebody's turn.
  if (said === "queued" || said === "waiting_for_input") return "warn";
  return "";
}

/** The class list for a mark in that tone, so no caller composes one for itself. */
export function markClass(tone: MarkTone): string {
  return tone.length > 0 ? `ec-mark ${tone}` : "ec-mark";
}
