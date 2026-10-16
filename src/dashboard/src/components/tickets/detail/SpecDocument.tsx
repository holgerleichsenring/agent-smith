"use client";

import Link from "next/link";
import { isRunLive } from "@/lib/runLiveness";
import { runPath } from "@/lib/runPath";
import type { TicketAttempt, TicketSeriesSpec } from "@/types/tickets";
import { BuildNote } from "./BuildNote";
import { CriterionRejection } from "./CriterionRejection";
import { ReviewRounds } from "./ReviewRounds";
import { criterionRow, overruleWords, recordNoteWords, specStatePill } from "./specWords";

// 2026-10-13-9a59r: one spec as a document — spec n, state, repository, the attempt that executed it, the
// goal as heading (no separate title is served), the file stem, steps as worked (9a59f: the attempt and
// pass that completed each), done when, the build note (8762m), the review rounds (8762l) and the read-only
// note. 2026-10-16-86e6d: "executed in A{n}" opens that attempt's run. A spec read from the run record has no file stem and nothing to discuss, and one without a readable
// record says only its goal was kept.

interface Props {
  spec: TicketSeriesSpec;
  attempts: TicketAttempt[];
  /** True for a spec of a run-record series. */
  runRecord?: boolean;
  onDiscuss: () => void;
}

export function SpecDocument({ spec, attempts, runRecord = false, onDiscuss }: Props) {
  const [pill, word] = specStatePill(spec.state);
  const live = attempts.some((a) => a.runId === spec.executedBy && isRunLive(a.status));
  const recordNote = recordNoteWords(spec);
  return (
    <div className="spdoc" aria-live="polite" data-testid="spec-doc">
      <div className="sh">
        <div className="meta">
          <span className="mono">spec {spec.ordinal}</span>
          <span className={`pill ${pill}`}>{word}</span>
          {spec.repos.length > 0 && <span className="mono">{spec.repos.join(", ")}</span>}
          {spec.executedByAttempt != null && spec.executedBy
            ? <Link href={runPath(spec.executedBy)} data-testid="spec-doc-executed">executed in A{spec.executedByAttempt}</Link>
            : <span>not executed yet</span>}
        </div>
        <h3 data-testid="spec-doc-goal">{spec.goal}</h3>
        {spec.fileStem && <div className="meta" data-testid="spec-doc-stem"><span className="mono">{spec.fileStem}.yaml</span></div>}
        {recordNote && <div className="meta" data-testid="spec-doc-record-note"><span>{recordNote}</span></div>}
      </div>
      <div className="sb">
        <div className="blk">
          <h4>Steps · as worked</h4>
          <div className="steps">
            {spec.steps.map((step, i) => {
              const worked = spec.worked?.find((w) => w.stepId === step.id);
              const cls = worked?.state === "done" ? (spec.state === "carried" ? "carried" : "done")
                : live && worked?.state ? "live" : "idle";
              const where = worked?.doneByAttempt != null
                ? `A${worked.doneByAttempt}${worked.doneInPass != null ? ` · pass ${worked.doneInPass}` : ""}` : "";
              return (
                <div key={step.id} className={`stp ${cls}`} data-testid={`spec-step-${step.id}`}>
                  <span className="sk">{cls === "done" ? "✓" : ""}</span>
                  <span className="sn2">{i + 1}</span>
                  <span>{step.action}</span>
                  <span className="sw">{where}</span>
                </div>
              );
            })}
          </div>
        </div>
        <div className="blk">
          <h4>Done when</h4>
          <div className="crit">
            {spec.done.map((criterion) => {
              const row = criterionRow(criterion, spec);
              const overruled = overruleWords(criterion);
              return (
                <div key={criterion.text} className="ci">
                  <span className={`m ${row.mark}`}>{row.glyph}</span>
                  <span>
                    {criterion.text}{overruled && <span className="dim-line"> · {overruled}</span>}
                    <CriterionRejection criterion={criterion} judgedBy={spec.judgedBy} />
                  </span>
                  <span className="cw">{row.who}</span>
                </div>
              );
            })}
          </div>
        </div>
        {spec.buildNote && <BuildNote note={spec.buildNote} />}
        <ReviewRounds specId={spec.specId} />
        {!runRecord && (
          <div className="ro-note">
            <span>Read-only. A spec changes only through the ticket conversation, and an executed spec never changes.</span>
            <button className="btn sm" type="button" onClick={onDiscuss} data-testid="spec-discuss">Discuss in conversation</button>
          </div>
        )}
      </div>
    </div>
  );
}
