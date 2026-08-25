"use client";

import { useState } from "react";
import type { CriterionDisposition, CriterionJudgement } from "@/lib/judgementsApi";

/**
 * 2026-08-25-e257: where the operator says a criterion's disposition was wrong.
 *
 * A LABEL, not a control. Recording one does not re-open the gate, re-run the phase or move
 * the run's state — a button that also shipped things would be pressed for reasons that have
 * nothing to do with whether the account was right, and the corpus would then measure
 * impatience.
 *
 * The reason is required, because a label nobody can audit later is worse than no label.
 */

const DISPOSITIONS: { value: CriterionDisposition; label: string }[] = [
  { value: "met", label: "it was met" },
  { value: "unmet", label: "it was not met" },
  { value: "not_applicable", label: "it did not apply" },
  { value: "unproven", label: "nothing measured it" },
];

const STATUS_LABEL: Record<CriterionDisposition, string> = {
  met: "met",
  unmet: "not met",
  not_applicable: "not applicable",
  unproven: "unproven",
};

interface Props {
  criterion: string;
  machineStatus: CriterionDisposition;
  judgement?: CriterionJudgement;
  onRecord: (humanStatus: CriterionDisposition, reason: string) => void;
  onWithdraw: () => void;
  busy?: boolean;
}

export function CriterionJudgementControl({
  criterion,
  machineStatus,
  judgement,
  onRecord,
  onWithdraw,
  busy,
}: Props) {
  const [open, setOpen] = useState(false);
  const [humanStatus, setHumanStatus] = useState<CriterionDisposition>(
    machineStatus === "met" ? "unmet" : "met",
  );
  const [reason, setReason] = useState("");

  if (judgement) {
    return (
      <div className="c-proof" data-testid="criterion-judgement">
        <b>{judgement.author} says: {STATUS_LABEL[judgement.humanStatus]}</b> — {judgement.reason}
        <button
          type="button"
          className="btn-link"
          data-testid="criterion-judgement-withdraw"
          disabled={busy}
          onClick={onWithdraw}
        >
          withdraw
        </button>
      </div>
    );
  }

  if (!open) {
    return (
      <button
        type="button"
        className="btn-link"
        data-testid="criterion-judgement-open"
        onClick={() => setOpen(true)}
      >
        this verdict is wrong
      </button>
    );
  }

  const canSave = reason.trim().length > 0 && humanStatus !== machineStatus;
  return (
    <div className="c-proof" data-testid="criterion-judgement-form">
      <label>
        Actually:{" "}
        <select
          data-testid="criterion-judgement-status"
          value={humanStatus}
          onChange={(e) => setHumanStatus(e.target.value as CriterionDisposition)}
        >
          {DISPOSITIONS.filter((d) => d.value !== machineStatus).map((d) => (
            <option key={d.value} value={d.value}>
              {d.label}
            </option>
          ))}
        </select>
      </label>
      <textarea
        data-testid="criterion-judgement-reason"
        placeholder="Why — what the account missed, and where to see it"
        value={reason}
        onChange={(e) => setReason(e.target.value)}
      />
      <div className="hint">
        This records a judgement about <i>{criterion.slice(0, 60)}…</i>. It changes nothing
        about the run.
      </div>
      <button
        type="button"
        data-testid="criterion-judgement-save"
        disabled={!canSave || busy}
        onClick={() => onRecord(humanStatus, reason.trim())}
      >
        record
      </button>
      <button type="button" className="btn-link" onClick={() => setOpen(false)}>
        cancel
      </button>
    </div>
  );
}
