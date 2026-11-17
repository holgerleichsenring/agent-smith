"use client";

import { useMemo, useState, type ReactNode } from "react";
import {
  type RunBeats,
  type RunEvent,
  type RunSnapshot,
} from "@/types/hub-events";
import { cn } from "@/lib/utils";
import { monotonizeBeats } from "@/lib/beatMonotonic";
import { StoryBar, type BeatKey, type StoryPhase, BEAT_ORDER } from "./StoryBar";
import { LedgerPanel } from "./LedgerPanel";
import { VerifySummary } from "./VerifySummary";
import { buildVerifyFallback } from "./verifyFallback";
import { TicketPanel } from "./TicketPanel";
import { BuildNotes } from "./BuildNotes";
import { SpecsPanel } from "./SpecsPanel";
import { OutcomePanel } from "./OutcomePanel";
import { PlanPanel, SpecPanel } from "./SpecPlanPanels";
import { runLedger } from "./runLedger";
import type { RunJudgements } from "./useRunJudgements";
import { isRelaunching } from "@/components/jobs/runStatus";

// p0344b/p0343c: the run as a STORY with the run-viewer.html mock's
// BEAT-SWITCHED STAGE. The storybar renders the SERVER-computed beats (a run
// without beats shows no storybar at all — honest, no guessing); clicking a
// beat switches the stage panel: Ticket = the TicketFetched body, Plan = the
// cached plan.md, Building = the persisted p0341 progress ledger + latest
// decisions/changes from the event stream, Verify = the per-criterion
// acceptance dispositions, Outcome = result.md + PR + the cost/wall-clock/LLM
// kv strip. The section-head above the stage names the current beat.

const BEAT_NAMES: Record<BeatKey, string> = {
  ticket: "The ticket",
  plan: "The plan",
  building: "Building",
  verify: "Verify against acceptance",
  outcome: "Outcome",
};

const BEAT_ICONS: Record<BeatKey, string> = {
  ticket: "◎",
  plan: "❋",
  building: "◧",
  verify: "✓",
  outcome: "▲",
};

// 2026-10-12-6bfad: the verify glyph IS a verdict, so it follows the beat — a ✓ beside a
// "failed" badge said both. The other glyphs only name their beat.
function sectionIcon(beat: BeatKey, state: string): string {
  if (beat !== "verify") return BEAT_ICONS[beat];
  if (state === "done") return "✓";
  return state === "failed" ? "✗" : "○";
}

interface RunStoryProps {
  runId: string;
  snapshot: RunSnapshot | null;
  events: RunEvent[];
  /** The attention banner (paused/failed/cancelled), rendered atop the stage. */
  banner?: ReactNode;
  /** The mock's right .sidebox (metrics + drawer entry points). */
  sidebox?: ReactNode;
  /** 2026-11-17-d6fbb: the page's one read of the run's judgements, shared with the run log so an overrule made in
   *  either view shows in both. */
  judged: RunJudgements;
}

export function RunStory({ runId, snapshot, events, banner, sidebox, judged }: RunStoryProps) {
  const fallback = useMemo(() => buildVerifyFallback(events), [events]);
  const [picked, setPicked] = useState<BeatKey | null>(null);

  // p0355: the server-computed beats can arrive non-monotonic (Building "done"
  // while The plan is still "active"). Clamp to a monotonic sequence before
  // rendering — the spine must never show a later beat ahead of an earlier one.
  const beats = useMemo(
    () => (snapshot?.beats ? monotonizeBeats(snapshot.beats) : null),
    [snapshot?.beats],
  );
  // p0355: the ledger is served on the REST detail (snapshot.progressLedger),
  // but while the detail is in flight — or if that fetch failed — the row falls
  // back to the list snapshot, which omits it. Recover it from the persisted
  // RunStoryRecorded event on the stream so a finished run does not show a false
  // "no ledger" negative.
  const ledger = useMemo(
    () => runLedger(snapshot, events),
    [snapshot, events],
  );
  const hasLedger = !!ledger && ledger.length > 0;
  // 2026-09-22-7c41c: a third state, never a negated flag. A parked run whose relaunch is
  // under way is executing nothing, so it is not "in progress"; it is also no longer asking,
  // so it is not "needs you". Both readings would have been wrong.
  const phase: StoryPhase = storyPhase(snapshot);

  const selected: BeatKey = picked ?? defaultBeat(beats);
  const subs = useMemo(() => beatSubs(snapshot, beats, phase), [snapshot, beats, phase]);

  return (
    <div data-testid="run-story">
      {/* the storybar spans the full width, ABOVE the stage/side grid */}
      {beats && (
        <StoryBar
          beats={beats}
          subs={subs}
          selected={selected}
          phase={phase}
          onBeatClick={setPicked}
        />
      )}

      <div className="grid">
        <div className="stage">
          {banner}
          {beats ? (
            <>
          <div className="section-head" data-testid="beat-section-head">
            <div
              className={cn("bh-ic", beats[selected] === "failed" && "bad")}
              data-testid="beat-section-icon"
            >
              {sectionIcon(selected, beats[selected])}
            </div>
            <div className="bh-t">
              <div className="n" data-testid="beat-section-name">{BEAT_NAMES[selected]}</div>
              <div className="s">{subs[selected]}</div>
            </div>
            <BeatBadge
              state={beats[selected]}
              phase={beats[selected] === "active" ? phase : "running"}
            />
          </div>

          <div data-panel={selected} data-testid={`beat-panel-${selected}`}>
            {selected === "ticket" && <TicketPanel snapshot={snapshot} events={events} />}
            {selected === "plan" && <><SpecPanel runId={runId} /><PlanPanel runId={runId} /></>}
            {selected === "building" && (
              <div className="stage">
                <section className="card">
                  <div className="card-h">
                    <h3>Progress ledger</h3>
                    {beats.building === "active" && <span className="badge run">● live</span>}
                  </div>
                  <div className="card-b">
                    {hasLedger ? (
                      <LedgerPanel entries={ledger!} />
                    ) : (
                      <p className="hint" data-testid="ledger-empty">
                        No persisted progress ledger on this run — it predates the durable ledger
                        or has not started building yet.
                      </p>
                    )}
                  </div>
                </section>
                {/* p0466: the specs this run executed, each openable once it has
                    ended — its decisions and the spec body it executed. */}
                <SpecsPanel runId={runId} revision={events.length} />
                <BuildNotes runId={runId} events={events} />
              </div>
            )}
            {selected === "verify" && (
              <VerifySummary
                acceptance={snapshot?.acceptance ?? null}
                fallback={fallback}
                judgements={judged.judgements}
                judgementBusy={judged.busy}
                onRecordJudgement={judged.record}
                onWithdrawJudgement={judged.withdraw}
              />
            )}
            {selected === "outcome" && <OutcomePanel runId={runId} snapshot={snapshot} />}
          </div>
        </>
      ) : (
        <>
          {/* Pre-beats run: no storybar, no stage switching — honest. The
              persisted artifacts that DO exist still render. */}
          {hasLedger && (
            <section className="card">
              <div className="card-h"><h3>Progress ledger</h3></div>
              <div className="card-b">
                <LedgerPanel entries={ledger!} />
              </div>
            </section>
          )}
          <VerifySummary
                acceptance={snapshot?.acceptance ?? null}
                fallback={fallback}
                judgements={judged.judgements}
                judgementBusy={judged.busy}
                onRecordJudgement={judged.record}
                onWithdrawJudgement={judged.withdraw}
              />
          <p className="hint" data-testid="story-no-beats">
            This run predates server-computed story beats — the full step trace is in “Full
            pipeline” on the right.
          </p>
        </>
          )}
        </div>
        {sidebox}
      </div>
    </div>
  );
}

// 2026-09-22-7c41c: what the spine reads off the run. A parked run's relaunch is under way
// when the server carried it a place in the capacity queue (or the 0 that says its launcher
// already holds the lease); everything else that is not parked is simply running.
function storyPhase(snapshot: RunSnapshot | null | undefined): StoryPhase {
  if (snapshot?.status !== "waiting_for_input") return "running";
  return isRelaunching(snapshot) ? "relaunching" : "needsYou";
}

const ACTIVE_BEAT_LABEL: Record<StoryPhase, string> = {
  running: "in progress",
  needsYou: "paused · needs you",
  relaunching: "resuming",
};

function BeatBadge({ state, phase }: { state: string; phase: StoryPhase }) {
  const map: Record<string, { cls: string; label: string }> = {
    done: { cls: "ok", label: "done" },
    active: { cls: "run", label: ACTIVE_BEAT_LABEL[phase] },
    failed: { cls: "bad", label: "failed" },
    pending: { cls: "neu", label: "not started" },
    skipped: { cls: "neu", label: "skipped" },
  };
  const badge = map[state] ?? map.pending;
  return (
    <span className={cn("badge", badge.cls)} data-testid="beat-section-badge">
      {badge.label}
    </span>
  );
}

// The beat whose panel shows by default: the first failed beat, else the
// active beat, else outcome when everything is done, else the ticket.
function defaultBeat(beats: RunSnapshot["beats"]): BeatKey {
  if (!beats) return "ticket";
  const failed = BEAT_ORDER.find((k) => beats[k] === "failed");
  if (failed) return failed;
  const active = BEAT_ORDER.find((k) => beats[k] === "active");
  if (active) return active;
  if (BEAT_ORDER.every((k) => beats[k] === "done" || beats[k] === "skipped")) return "outcome";
  return "ticket";
}

// Real, per-beat sub captions — derived from snapshot fields only. Beats are the
// monotonic-clamped set (p0355), not the raw snapshot beats.
function beatSubs(
  snapshot: RunSnapshot | null,
  beats: RunBeats | null,
  phase: StoryPhase,
): Record<BeatKey, string> {
  const base: Record<string, string> = {
    done: "Done",
    active: "In progress",
    failed: "Failed",
    pending: "Not started",
    skipped: "Skipped",
  };
  const subs = Object.fromEntries(
    BEAT_ORDER.map((k) => [k, beats ? base[beats[k]] ?? "" : ""]),
  ) as Record<BeatKey, string>;
  if (!snapshot || !beats) return subs;

  if (snapshot.ticketId && beats.ticket === "done") subs.ticket = `${snapshot.ticketId} · fetched`;
  const ledger = snapshot.progressLedger ?? [];
  if (beats.plan === "done" && ledger.length > 0) subs.plan = `${ledger.length} steps`;
  if (beats.building === "active") {
    subs.building =
      phase === "needsYou"
        ? "Paused — open question"
        : phase === "relaunching"
        ? "Answered — waiting to resume"
        : snapshot.totalSteps > 0
        ? `Step ${snapshot.stepIndex} of ${snapshot.totalSteps}`
        : "In progress";
  } else if (beats.building === "done" && ledger.length > 0) {
    const done = ledger.filter((e) => e.status === "done").length;
    subs.building = `${done} of ${ledger.length} done`;
  }
  const criteria = snapshot.acceptance?.criteria ?? [];
  // 2026-10-12-6bfad: an account nobody could take is not one of the N — "0 of 1 proven"
  // over a build that never ran counted the sandbox key as a criterion.
  const unmeasured = snapshot.acceptance?.unmeasured?.length ?? 0;
  if (criteria.length > 0) {
    const met = criteria.filter((c) => c.status === "met").length;
    subs.verify = `${met} of ${criteria.length} proven${unmeasured > 0 ? " · not measured" : ""}`;
  } else if (unmeasured > 0) {
    subs.verify = "Not measured";
  } else if (beats.verify === "pending") {
    subs.verify = "Against acceptance";
  }
  if (beats.outcome === "done") subs.outcome = snapshot.prUrl ? "PR opened" : "Report written";
  else if (beats.outcome === "pending") subs.outcome = "PR & report";
  return subs;
}
