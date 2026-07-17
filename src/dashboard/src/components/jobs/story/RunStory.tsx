"use client";

import { useMemo, useState, type ReactNode } from "react";
import type { RunEvent, RunSnapshot } from "@/types/hub-events";
import { cn } from "@/lib/utils";
import { StoryBar, type BeatKey, BEAT_ORDER } from "./StoryBar";
import { LedgerPanel } from "./LedgerPanel";
import { VerifySummary } from "./VerifySummary";
import { buildVerifyFallback } from "./verifyFallback";
import { TicketPanel } from "./TicketPanel";
import { BuildNotes } from "./BuildNotes";
import { OutcomePanel } from "./OutcomePanel";
import { usePlanMarkdown } from "@/hooks/usePlanMarkdown";
import { ResultDocument } from "@/components/jobs/ResultTab";

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

interface RunStoryProps {
  runId: string;
  snapshot: RunSnapshot | null;
  events: RunEvent[];
  /** The attention banner (paused/failed/cancelled), rendered atop the stage. */
  banner?: ReactNode;
  /** The mock's right .sidebox (metrics + drawer entry points). */
  sidebox?: ReactNode;
}

export function RunStory({ runId, snapshot, events, banner, sidebox }: RunStoryProps) {
  const fallback = useMemo(() => buildVerifyFallback(events), [events]);
  const [picked, setPicked] = useState<BeatKey | null>(null);

  const beats = snapshot?.beats ?? null;
  const ledger = snapshot?.progressLedger ?? null;
  const hasLedger = !!ledger && ledger.length > 0;
  const paused = snapshot?.status === "waiting_for_input";

  const selected: BeatKey = picked ?? defaultBeat(beats);
  const subs = useMemo(() => beatSubs(snapshot, paused), [snapshot, paused]);

  return (
    <div data-testid="run-story">
      {/* the storybar spans the full width, ABOVE the stage/side grid */}
      {beats && (
        <StoryBar
          beats={beats}
          subs={subs}
          selected={selected}
          paused={paused}
          onBeatClick={setPicked}
        />
      )}

      <div className="grid">
        <div className="stage">
          {banner}
          {beats ? (
            <>
          <div className="section-head" data-testid="beat-section-head">
            <div className="bh-ic">{BEAT_ICONS[selected]}</div>
            <div className="bh-t">
              <div className="n" data-testid="beat-section-name">{BEAT_NAMES[selected]}</div>
              <div className="s">{subs[selected]}</div>
            </div>
            <BeatBadge state={beats[selected]} paused={paused && beats[selected] === "active"} />
          </div>

          <div data-panel={selected} data-testid={`beat-panel-${selected}`}>
            {selected === "ticket" && <TicketPanel snapshot={snapshot} events={events} />}
            {selected === "plan" && <PlanPanel runId={runId} />}
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
                <BuildNotes events={events} />
              </div>
            )}
            {selected === "verify" && (
              <VerifySummary acceptance={snapshot?.acceptance ?? null} fallback={fallback} />
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
          <VerifySummary acceptance={snapshot?.acceptance ?? null} fallback={fallback} />
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

// The Plan beat's stage — the cached plan.md, in the mock's card chrome.
function PlanPanel({ runId }: { runId: string }) {
  const { content, loading } = usePlanMarkdown(runId);
  return (
    <section className="card" data-testid="plan-panel">
      <div className="card-h">
        <h3>The agreed plan</h3>
      </div>
      <div className="card-b">
        {content ? (
          <div data-testid="plan-markdown">
            <ResultDocument content={content} />
          </div>
        ) : (
          <p className="hint">
            {loading
              ? "Loading…"
              : "No plan recorded for this run yet — the master writes plan.md during the run; it appears here once cached."}
          </p>
        )}
      </div>
    </section>
  );
}

function BeatBadge({ state, paused }: { state: string; paused: boolean }) {
  const map: Record<string, { cls: string; label: string }> = {
    done: { cls: "ok", label: "done" },
    active: paused
      ? { cls: "run", label: "paused · needs you" }
      : { cls: "run", label: "in progress" },
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

// Real, per-beat sub captions — derived from snapshot fields only.
function beatSubs(snapshot: RunSnapshot | null, paused: boolean): Record<BeatKey, string> {
  const base: Record<string, string> = {
    done: "Done",
    active: "In progress",
    failed: "Failed",
    pending: "Not started",
    skipped: "Skipped",
  };
  const beats = snapshot?.beats;
  const subs = Object.fromEntries(
    BEAT_ORDER.map((k) => [k, beats ? base[beats[k]] ?? "" : ""]),
  ) as Record<BeatKey, string>;
  if (!snapshot || !beats) return subs;

  if (snapshot.ticketId && beats.ticket === "done") subs.ticket = `${snapshot.ticketId} · fetched`;
  const ledger = snapshot.progressLedger ?? [];
  if (beats.plan === "done" && ledger.length > 0) subs.plan = `${ledger.length} steps`;
  if (beats.building === "active") {
    subs.building = paused
      ? "Paused — open question"
      : snapshot.totalSteps > 0
      ? `Step ${snapshot.stepIndex} of ${snapshot.totalSteps}`
      : "In progress";
  } else if (beats.building === "done" && ledger.length > 0) {
    const done = ledger.filter((e) => e.status === "done").length;
    subs.building = `${done} of ${ledger.length} done`;
  }
  const criteria = snapshot.acceptance?.criteria ?? [];
  if (criteria.length > 0) {
    const met = criteria.filter((c) => c.status === "met").length;
    subs.verify = `${met} of ${criteria.length} proven`;
  } else if (beats.verify === "pending") {
    subs.verify = "Against acceptance";
  }
  if (beats.outcome === "done") subs.outcome = snapshot.prUrl ? "PR opened" : "Report written";
  else if (beats.outcome === "pending") subs.outcome = "PR & report";
  return subs;
}
