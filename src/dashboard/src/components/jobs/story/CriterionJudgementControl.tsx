"use client";

import { useState } from "react";
import type { CriterionDisposition, CriterionJudgement } from "@/lib/judgementsApi";
import { useT } from "@/hooks/useT";
import type { TextKey } from "@/lib/i18n/translate";

/**
 * 2026-08-25-e257: where the operator says a criterion's disposition was wrong.
 *
 * A LABEL, not a control. Recording one does not re-open the gate, re-run the spec or move
 * the run's state — a button that also shipped things would be pressed for reasons that have
 * nothing to do with whether the account was right, and the corpus would then measure
 * impatience.
 *
 * The reason is required, because a label nobody can audit later is worse than no label.
 */

const DISPOSITIONS: { value: CriterionDisposition; label: TextKey }[] = [
  { value: "met", label: "runs.story.judge.met" },
  { value: "unmet", label: "runs.story.judge.unmet" },
  { value: "not_applicable", label: "runs.story.judge.not_applicable" },
  { value: "unproven", label: "runs.story.judge.unproven" },
];

const STATUS_LABEL: Record<CriterionDisposition, TextKey> = {
  met: "runs.story.judgeStatus.met",
  unmet: "runs.story.judgeStatus.unmet",
  not_applicable: "runs.story.judgeStatus.not_applicable",
  unproven: "runs.story.judgeStatus.unproven",
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
  const t = useT();
  const [open, setOpen] = useState(false);
  const [humanStatus, setHumanStatus] = useState<CriterionDisposition>(
    machineStatus === "met" ? "unmet" : "met",
  );
  const [reason, setReason] = useState("");

  if (judgement) {
    return (
      <div className="c-proof" data-testid="criterion-judgement">
        <b>{t("runs.story.judge.says", { author: judgement.author, status: t(STATUS_LABEL[judgement.humanStatus]) })}</b> — {judgement.reason}
        <button
          type="button"
          className="btn-link"
          data-testid="criterion-judgement-withdraw"
          disabled={busy}
          onClick={onWithdraw}
        >
          {t("runs.story.judge.withdraw")}
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
        {t("runs.story.judge.open")}
      </button>
    );
  }

  const canSave = reason.trim().length > 0 && humanStatus !== machineStatus;
  return (
    <div className="c-proof" data-testid="criterion-judgement-form">
      <label>
        {t("runs.story.judge.actually")}{" "}
        <select
          data-testid="criterion-judgement-status"
          value={humanStatus}
          onChange={(e) => setHumanStatus(e.target.value as CriterionDisposition)}
        >
          {DISPOSITIONS.filter((d) => d.value !== machineStatus).map((d) => (
            <option key={d.value} value={d.value}>
              {t(d.label)}
            </option>
          ))}
        </select>
      </label>
      <textarea
        data-testid="criterion-judgement-reason"
        placeholder={t("runs.story.judge.reason")}
        value={reason}
        onChange={(e) => setReason(e.target.value)}
      />
      <div className="hint">
        {t("runs.story.judge.records.a")}<i>{criterion.slice(0, 60)}…</i>{t("runs.story.judge.records.b")}
      </div>
      <button
        type="button"
        data-testid="criterion-judgement-save"
        disabled={!canSave || busy}
        onClick={() => onRecord(humanStatus, reason.trim())}
      >
        {t("runs.story.judge.record")}
      </button>
      <button type="button" className="btn-link" onClick={() => setOpen(false)}>
        {t("runs.story.judge.cancel")}
      </button>
    </div>
  );
}
