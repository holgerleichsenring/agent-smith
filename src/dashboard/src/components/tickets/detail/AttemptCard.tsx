"use client";

import Link from "next/link";
import { useState } from "react";
import { isRunLive } from "@/lib/runLiveness";
import { runPath, runTracePath } from "@/lib/runPath";
import type { Read } from "@/lib/tickets/TicketPageReads";
import { useTicketSeries } from "@/lib/tickets/TicketPageStore";
import type { RunSnapshot } from "@/types/hub-events";
import { RUN_END_KINDS, type RunCompute, type RunPasses, type TicketAttempt, type TicketConversationEntry, type TicketSeries } from "@/types/tickets";
import { money } from "../ticketFormat";
import { attemptStory, causeLabel, causeWhere, liveSpec, questionWaits } from "./attemptStory";
import { duration } from "./computeTotals";
import { elapsed } from "./flowWords";

// 2026-10-13-9a59s: one attempt on the Attempts timeline — the mock's .att card. The head is a button that
// opens the body; the live attempt opens by itself. A handed-back attempt that has ended reads its own cut
// (?runId=) once, for the hand-back case its Ended line names.

interface Props {
  attempt: TicketAttempt;
  passes: Read<RunPasses>;
  compute: Read<RunCompute>;
  run: Read<RunSnapshot>;
  cutEntries: TicketConversationEntry[];
  /** The ticket's default cut — the live attempt's "spec n of m" only when it is this attempt's own. */
  series: TicketSeries | null;
  now: number;
}

const PILLS: Record<string, [string, string]> = {
  running: ["amber", "running"], queued: ["amber", "queued"], waiting_for_input: ["person", "waiting for an answer"],
  success: ["ok", "succeeded"], shortfall: ["amber", "shortfall"], failed: ["bad", "failed"], error: ["bad", "failed"],
  cancelled: ["q", "cancelled"],
};

export function outcomePill(attempt: TicketAttempt): [string, string] {
  // 2026-10-16-86e6b: a park a newer attempt started past is closed as superseded, whatever it waited on.
  if (attempt.endKind === RUN_END_KINDS.superseded) return ["q", "superseded"];
  if (attempt.handedBack) return ["person", "handed back"];
  if (attempt.endedOnInfrastructure) return ["bad", "ended on infrastructure"];
  return PILLS[attempt.status] ?? ["q", attempt.status.replace(/_/g, " ")];
}

function dotClass(attempt: TicketAttempt): string {
  if (attempt.handedBack) return "hb";
  if (isRunLive(attempt.status)) return "run";
  if (attempt.status === "success" || attempt.status === "shortfall") return "ok";
  return attempt.status === "cancelled" ? "" : "bad";
}

export function AttemptCard({ attempt, passes, compute, run, cutEntries, series, now }: Props) {
  const live = isRunLive(attempt.status) && !attempt.handedBack;
  const [open, setOpen] = useState(live);
  const own = useTicketSeries(attempt.handedBack && !isRunLive(attempt.status) ? attempt.runId : null);
  const cut = attempt.handedBack ? own.data : series;
  const story = attemptStory({ attempt, passes: passes.data, cutEntries, cut });
  const [pillClass, pillText] = outcomePill(attempt);
  const bodyId = `attempt-body-${attempt.number}`;
  const llmCalls = run.data?.llmCalls;

  return (
    <div className={`att ${dotClass(attempt)}`} data-testid={`attempt-${attempt.number}`}>
      <span className="dot" />
      <div className="card">
        <button className="a-top" type="button" aria-expanded={open} aria-controls={bodyId} onClick={() => setOpen(!open)}
          data-testid={`attempt-head-${attempt.number}`}>
          <div>
            <div className="a-name">
              <span className="an">Attempt {attempt.number}</span>
              <span className={`pill ${pillClass}`}>{pillText}</span>
              <span className="rid">{attempt.runId}</span>
            </div>
            <WhyLine attempt={attempt} compute={compute.data} now={now} />
          </div>
          <div className="a-num">{elapsed(attempt.startedAt, attempt.finishedAt)}<small>{live ? "running" : "wall"}</small></div>
          <div className="a-num hidesm">{money(attempt.cost)}<small>{live ? "so far" : "total"}</small></div>
          <SpecsNumber attempt={attempt} live={live} cut={cut} />
        </button>
        <div className="a-body" id={bodyId} hidden={!open} data-testid={`attempt-body-${attempt.number}`}>
          {story.doing && <Kv k="Doing" lines={story.doing} testId="attempt-doing" />}
          {story.did && <Kv k="Did" lines={story.did} testId="attempt-did" />}
          {story.ended && (
            <div className="kv"><span className="k">Ended</span>
              <div className="v" data-testid="attempt-ended"><b>{story.ended.lead}</b>{story.ended.rest ? ` ${story.ended.rest}` : ""}</div>
            </div>
          )}
          <div className="kv"><span className="k">Machine</span>
            <div className="v links">
              <Link className="btn sm" href={runPath(attempt.runId)} data-testid="attempt-viewer">Open run viewer ↗</Link>
              <Link className="btn sm" href={runTracePath(attempt.runId)} data-testid="attempt-trace">
                Trace{typeof llmCalls === "number" ? ` · ${llmCalls} LLM calls` : ""}
              </Link>
            </div>
          </div>
        </div>
      </div>
    </div>
  );
}

function Kv({ k, lines, testId }: { k: string; lines: string[]; testId: string }) {
  return (
    <div className="kv"><span className="k">{k}</span>
      <div className="v"><ul data-testid={testId}>{lines.map((line) => <li key={line}>{line}</li>)}</ul></div>
    </div>
  );
}

function WhyLine({ attempt, compute, now }: { attempt: TicketAttempt; compute: RunCompute | null; now: number }) {
  const label = causeLabel(attempt);
  const who = [attempt.cause?.actor, causeWhere(attempt)].filter(Boolean).join(" on ");
  const carried = attempt.revision?.carriedIds.length ?? 0;
  const parks = (compute?.segments ?? []).flatMap((s) => (s.park ? [s.park] : []));
  const asked = questionWaits(parks, now);
  const parts: React.ReactNode[] = [];
  if (label) parts.push(<span key="cause">started by <span className="cause">{label}</span></span>);
  if (who) parts.push(<span key="who">{who}</span>);
  if (carried > 0) parts.push(<span key="carried">{carried === 1 ? "1 spec carried" : `${carried} specs carried`}</span>);
  if (asked) {
    parts.push(<b key="asked">asked {asked.count === 1 ? "1 question" : `${asked.count} questions`}, waited {duration(asked.ms)}</b>);
  }
  if (parts.length === 0) return null;
  return (
    <div className="a-why" data-testid="attempt-why">
      {parts.flatMap((part, i) => (i === 0 ? [part] : [<span key={`sep-${i}`}> · </span>, part]))}
    </div>
  );
}

function SpecsNumber({ attempt, live, cut }: { attempt: TicketAttempt; live: boolean; cut: TicketSeries | null }) {
  if (live) {
    const on = liveSpec(attempt, cut);
    return (
      <div className="a-num hidesm" data-testid="attempt-specs">
        {on ? `spec ${on.spec.ordinal} of ${on.of}` : "deriving"}
        <small>{on?.step ? `step ${on.step} of ${on.steps}` : on ? "working" : "no cut of its own yet"}</small>
      </div>
    );
  }
  const specs = attempt.executedSpecs;
  if (specs.length === 0) return <div className="a-num hidesm" data-testid="attempt-specs">—<small>no spec cut</small></div>;
  const done = specs.filter((s) => s.status === "done").length;
  return <div className="a-num hidesm" data-testid="attempt-specs">{done} / {specs.length}<small>specs done</small></div>;
}
