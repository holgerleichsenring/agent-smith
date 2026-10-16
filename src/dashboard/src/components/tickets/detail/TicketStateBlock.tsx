"use client";

import Link from "next/link";
import { ProjectInitAction } from "@/components/system/ProjectInitAction";
import { formatRunSummary } from "@/lib/formatRunSummary";
import { runPath, runTracePath } from "@/lib/runPath";
import { useLiveAttempt, useTicketSeries } from "@/lib/tickets/TicketPageStore";
import type { TicketDetail } from "@/types/tickets";
import { RetryUnchanged } from "./RetryUnchanged";
import { ticketState, type StateAct, type TicketStateView } from "./ticketState";
import { CAUSE_PARAM, useTicketTabGo } from "./useTicketTabGo";

// 2026-10-16-86e6c: the state block under the header, on every tab. Visible by default: what is going on (pill and
// one sentence), the way to the run it is about, and the one act that fits. One click away: other options, and Why —
// the run's own end text verbatim with its run and trace. A healthy state is one quiet line.

export function TicketStateBlock({ detail }: { detail: TicketDetail }) {
  const live = useLiveAttempt();
  const series = useTicketSeries().data;
  const view = ticketState({ detail, liveEvents: live.events, series });
  if (!view) return null;
  return (
    <section className={`tstate ${view.tone}`} data-testid="ticket-state" data-tone={view.tone} aria-live="polite">
      <div className="ts-top">
        <span className={`pill ${view.tone}`} data-testid="ticket-state-pill">{view.pill}</span>
        <div className="ts-say">
          <p className="ts-sentence" data-testid="ticket-state-sentence">{view.sentence}</p>
          {view.detail && <div className="ts-detail" data-testid="ticket-state-detail">{view.detail}</div>}
        </div>
        {view.attempt && (
          <Link className="btn sm" href={runPath(view.attempt.runId)} data-testid="ticket-state-run">
            Open run A{view.attempt.number} ↗
          </Link>
        )}
      </div>
      {!view.quiet && <Acts view={view} project={detail.summary.project} />}
      {!view.quiet && view.tone === "bad" && <Why view={view} />}
    </section>
  );
}

function Acts({ view, project }: { view: TicketStateView; project: string }) {
  if (!view.act && view.others.length === 0) return null;
  return (
    <div className="ts-acts" data-testid="ticket-state-acts">
      {view.act && <Act act={view.act} project={project} primary />}
      {view.others.length > 0 && (
        <details className="ts-more" data-testid="ticket-state-others">
          <summary>Other options</summary>
          <div className="ts-more-body">
            {view.others.map((act, i) => <Act key={i} act={act} project={project} primary={false} />)}
          </div>
        </details>
      )}
    </div>
  );
}

function Act({ act, project, primary }: { act: StateAct; project: string; primary: boolean }) {
  const address = useTicketTabGo();
  const cls = primary ? "btn primary" : "btn sm";
  switch (act.kind) {
    case "retry": return <RetryUnchanged attempt={act.attempt} project={project} />;
    case "init": return <span className="ts-act" data-testid="ticket-state-init"><ProjectInitAction project={act.project} /></span>;
    case "link":
      return (
        <span className="ts-act">
          <Link className={cls} href={act.href} data-testid="ticket-state-act">{act.label}</Link>
          {act.note && <span className="cp-note">{act.note}</span>}
        </span>
      );
    case "tab":
      return (
        <span className="ts-act">
          <button type="button" className={cls} data-testid={primary ? "ticket-state-act" : "ticket-state-other"}
            onClick={() => address.go(act.tab, { [CAUSE_PARAM]: null })}>{act.label}</button>
          {act.note && <span className="cp-note">{act.note}</span>}
        </span>
      );
  }
}

function Why({ view }: { view: TicketStateView }) {
  const attempt = view.attempt;
  if (!attempt) return null;
  return (
    <details className="ts-why" data-testid="ticket-state-why">
      <summary>Why</summary>
      <div className="ts-why-body">
        {attempt.summary
          ? <pre className="ts-cause" data-testid="ticket-state-cause">{formatRunSummary(attempt.summary)}</pre>
          : <span className="cp-note">The run recorded no end text.</span>}
        <span className="ts-links">
          <Link className="btn sm" href={runPath(attempt.runId)}>Run A{attempt.number}</Link>
          <Link className="btn sm" href={runTracePath(attempt.runId)} data-testid="ticket-state-trace">Trace</Link>
        </span>
      </div>
    </details>
  );
}
