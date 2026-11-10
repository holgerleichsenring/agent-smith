"use client";

import { useState, type ReactNode } from "react";
import type { SpecDialogProposalFinding, SpecDialogReviewRound } from "@/types/spec-dialog";
import { DialogProposalRounds } from "./DialogProposalRounds";
import { STATUS_LABEL, type SpecStatus, type TurnDraft } from "./turnProposal";

// 2026-11-10-d854b: one spec of a turn's result, closed by default: its id, goal, repository and a status pill.
// Opened, it shows what the review found and when it is done, with its full yaml, its facts and its review rounds
// one click further — nothing of the proposal is dropped, only folded.

type Fold = "yaml" | "facts" | "rounds" | null;

export function TurnSpecRow({
  draft,
  status,
  findings,
  rounds,
  extra,
}: {
  draft: TurnDraft;
  status: SpecStatus;
  findings: SpecDialogProposalFinding[];
  rounds: SpecDialogReviewRound[];
  /** The ticket page's own parts for this spec: its disposition and a ratified mock. */
  extra?: ReactNode;
}) {
  const [open, setOpen] = useState(false);
  const [fold, setFold] = useState<Fold>(null);
  const toggle = (next: Fold) => setFold((was) => (was === next ? null : next));
  return (
    <div className="d-turn-row" data-testid={`turn-row-${draft.specId}`} data-status={status}>
      <button type="button" className="d-turn-toggle" aria-expanded={open} data-testid="turn-row-toggle"
        onClick={() => setOpen((was) => !was)}>
        <span aria-hidden="true" className="dsh-label font-mono text-body">{open ? "▾" : "▸"}</span>
        <span className="dsh-label font-mono text-body">{draft.specId}</span>
        <span className="dsh-body min-w-0 flex-[1_1_280px]">{draft.goal}</span>
        {draft.repos.map((repo) => <span key={repo} className="d-repo">{repo}</span>)}
        <span className={`d-pill ${status}`} data-testid="turn-row-status">{STATUS_LABEL[status]}</span>
      </button>
      {extra && <div className="px-3 pb-2">{extra}</div>}
      {open && (
        <div className="open" data-testid="turn-row-open">
          {findings.length > 0 && (
            <div className="flex flex-col gap-1" data-testid="turn-row-findings">
              <span className="fl">Review findings</span>
              {findings.map((finding, index) => (
                <span key={index} className="text-ink">• {finding.problem}: {finding.why}</span>
              ))}
            </div>
          )}
          {draft.done.length > 0 && (
            <div className="flex flex-col gap-1" data-testid="turn-row-done">
              <span className="fl">Done when</span>
              {draft.done.map((line, index) => <span key={index} className="text-ink">✓ {line}</span>)}
            </div>
          )}
          <div className="flex flex-wrap gap-4">
            {draft.yaml && <Link label="Full spec (YAML)" on={() => toggle("yaml")} id="yaml" />}
            {(draft.facts?.length ?? 0) > 0 && <Link label="Facts and evidence" on={() => toggle("facts")} id="facts" />}
            {rounds.length > 0 && <Link label="Review rounds" on={() => toggle("rounds")} id="rounds" />}
          </div>
          {fold === "yaml" && <pre data-testid="turn-row-yaml">{draft.yaml}</pre>}
          {fold === "facts" && (
            <ul className="ml-4 list-disc" data-testid="turn-row-facts">
              {draft.facts!.map((fact, index) => <li key={index}>{fact.claim} — {fact.evidence}</li>)}
            </ul>
          )}
          {fold === "rounds" && <DialogProposalRounds rounds={rounds} />}
        </div>
      )}
    </div>
  );
}

function Link({ label, on, id }: { label: string; on: () => void; id: string }) {
  return (
    <button type="button" className="d-link" data-testid={`turn-row-link-${id}`} onClick={on}>
      {label}
    </button>
  );
}
