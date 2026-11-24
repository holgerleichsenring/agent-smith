"use client";

import Link from "next/link";
import { useEffect, useState } from "react";
import { commandKey, stepState, type SectionState } from "@/components/jobs/runlog/runSections";
import { stepDetailLines } from "@/components/jobs/runlog/stepDetailLines";
import { useRunStepEvents } from "@/hooks/useRunStepEvents";
import { useDateLocale, useT } from "@/hooks/useT";
import type { TextKey } from "@/lib/i18n/translate";
import { pocRevisionPagePath } from "@/lib/pocRevisionPath";
import { getPocRevision, getPocRollout, type PocRevisionView, type PocRolloutView } from "@/lib/pocsApi";
import { runPath } from "@/lib/runPath";
import { fetchRunSteps, type RunStepRow } from "@/lib/runStepsApi";
import { revisionSteps } from "../blocked/StepStrip";
import "@/styles/pocs.css";

// 2026-11-23-c81ec: /pocs/{pocId}/rollouts/{runId} — the steps of the run that deploys a PoC, in order, each with its
// live lines: validate, code scan, source commit, the host pipeline's three jobs, sealing, the GitOps commit and the
// rollout itself, whose lines are one per Argo CD reading. The PoC's address shows once the rollout settled healthy.
// The page re-reads while the run is live and stops once it has ended.

const POCS_LIST_PATH = "/pocs";

/** How often the page re-reads the rollout, the steps and the revision while the run is live. */
export const ROLLOUT_POLL_INTERVAL_MS = 3000;

/** The PoC steps this page shows, by command name, with their labels. Other steps of the run are left out. */
const STEP_LABELS: Record<string, TextKey> = {
  PocIntake: "pocs.rollout.step.intake",
  PocCodeScan: "pocs.rollout.step.codeScan",
  PocSourceCommit: "pocs.rollout.step.sourceCommit",
  PocPipelineBuild: "pocs.rollout.step.pipelineBuild",
  SealPocVariables: "pocs.rollout.step.seal",
  GitOpsCommit: "pocs.rollout.step.gitopsCommit",
  PocRolloutFollow: "pocs.rollout.step.follow",
};

const PIPELINE_JOBS = new Set(["build", "imageScan", "push"]);

type Load =
  | { status: "loading" }
  | { status: "missing" }
  | { status: "failed"; message: string }
  | { status: "ok"; view: PocRolloutView; steps: RunStepRow[]; revision: PocRevisionView | null };

/** The run has ended once the rollout settled, a step failed, or every step was reached and none is running. */
export function rolloutEnded(view: PocRolloutView, steps: RunStepRow[]): boolean {
  if (view.rolloutState !== null) return true;
  if (steps.some((s) => stepState(s) === "bad")) return true;
  return steps.length > 0 && steps.every((s) => !s.planned && !!s.status && s.status !== "running");
}

export function PocRolloutPage({ pocId, runId }: { pocId: number; runId: string }) {
  const t = useT();
  const [load, setLoad] = useState<Load>({ status: "loading" });
  const valid = Number.isInteger(pocId) && pocId > 0 && runId.length > 0;

  useEffect(() => {
    if (!valid) return;
    const abort = new AbortController();
    let timer: ReturnType<typeof setTimeout> | undefined;
    const read = async () => {
      try {
        const view = await getPocRollout(pocId, runId, abort.signal);
        if (abort.signal.aborted) return;
        if (!view) {
          setLoad({ status: "missing" });
          return;
        }
        const [steps, revision] = await Promise.all([
          fetchRunSteps(runId, abort.signal).catch(() => [] as RunStepRow[]),
          view.revision !== null ? getPocRevision(pocId, view.revision, abort.signal).catch(() => null) : Promise.resolve(null),
        ]);
        if (abort.signal.aborted) return;
        setLoad({ status: "ok", view, steps, revision });
        if (!rolloutEnded(view, steps)) timer = setTimeout(() => void read(), ROLLOUT_POLL_INTERVAL_MS);
      } catch (err: unknown) {
        if (abort.signal.aborted) return;
        setLoad({ status: "failed", message: err instanceof Error ? err.message : String(err) });
      }
    };
    void read();
    return () => {
      abort.abort();
      if (timer) clearTimeout(timer);
    };
  }, [pocId, runId, valid]);

  if (load.status === "ok") return <PocRolloutDetail view={load.view} steps={load.steps} revision={load.revision} />;
  const sentence = !valid || load.status === "missing" ? t("pocs.rollout.notFound")
    : load.status === "failed" ? t("pocs.rollout.failed", { message: load.message })
    : t("pocs.rollout.loading");
  return (
    <>
      <div className="poc-crumb"><Link href={POCS_LIST_PATH}>{t("pocs.title")}</Link></div>
      <div className="msub note" data-testid={`poc-rollout-${!valid ? "missing" : load.status}`}>{sentence}</div>
    </>
  );
}

export function PocRolloutDetail({ view, steps, revision }: {
  view: PocRolloutView; steps: RunStepRow[]; revision: PocRevisionView | null;
}) {
  const t = useT();
  const live = !rolloutEnded(view, steps);
  const shown = steps.filter((s) => commandKey(s) in STEP_LABELS).sort((a, b) => a.stepIndex - b.stepIndex);
  const failed = view.rolloutState === "failed" || steps.some((s) => stepState(s) === "bad");
  const settled = view.rolloutState === "healthy";
  const badge = settled ? "ok" : failed ? "bad" : "run";
  const stateKey: TextKey = settled ? "pocs.rollout.state.healthy" : failed ? "pocs.rollout.state.failed" : "pocs.rollout.state.following";
  return (
    <div data-testid="poc-rollout" data-rollout={view.rolloutState ?? "following"}>
      <div className="poc-crumb">
        <Link href={POCS_LIST_PATH}>{t("pocs.title")}</Link> / {view.name} / {t("pocs.rollout.title")}
      </div>
      <div className="m-head">
        <div>
          <h1 className="poc-rev-title">
            {t("pocs.rollout.title")}
            <span className={`poc-badge ${badge}`} data-testid="poc-rollout-state">
              <span className="poc-badge-dot" aria-hidden="true" />{t(stateKey)}
            </span>
          </h1>
          <div className="msub" data-testid="poc-rollout-sub">
            {settled && view.url ? (
              <>{t("pocs.rollout.sub.healthy")}{" "}
                <a href={view.url} target="_blank" rel="noreferrer" data-testid="poc-rollout-url">{view.url} ↗</a></>
            ) : failed ? t("pocs.rollout.sub.failed") : t("pocs.rollout.sub.following")}
          </div>
          <div className="poc-meta">
            {view.revision !== null && (
              <Link href={pocRevisionPagePath(view.pocId, view.revision)} data-testid="poc-rollout-revision">
                {t("pocs.rollout.revision", { revision: view.revision })}
              </Link>
            )}{" "}
            <Link href={runPath(view.runId)} data-testid="poc-rollout-run">{t("pocs.revision.run")} →</Link>
          </div>
        </div>
      </div>
      <ol className="poc-rollout-steps" aria-label={t("pocs.revision.steps")} data-testid="poc-rollout-steps">
        {shown.map((row) => (
          // Keyed by status too: a step that ends is read again whole, so its last lines are not lost between polls.
          <RolloutStep key={`${row.stepIndex}:${row.status ?? "planned"}`} row={row} view={view} revision={revision} live={live} />
        ))}
      </ol>
    </div>
  );
}

const LOOK_KEYS: Record<SectionState, TextKey> = {
  pending: "pocs.rollout.look.pending",
  running: "pocs.rollout.look.running",
  done: "pocs.rollout.look.done",
  warn: "pocs.rollout.look.done",
  quiet: "pocs.rollout.look.done",
  bad: "pocs.rollout.look.bad",
};

function RolloutStep({ row, view, revision, live }: {
  row: RunStepRow; view: PocRolloutView; revision: PocRevisionView | null; live: boolean;
}) {
  const t = useT();
  const locale = useDateLocale();
  const key = commandKey(row);
  const state = row.planned ? "pending" : stepState(row);
  const reached = !row.planned && !!row.status;
  const { events } = useRunStepEvents(view.runId, reached ? row.stepIndex : null, live && row.status === "running");
  // stepDetailLines sorts newest first; a rollout reads top to bottom, oldest first.
  const lines = stepDetailLines(t, locale, events, !live).lines.slice().reverse();
  const testId = `poc-rollout-step-${key}`;
  return (
    <li className={`poc-rollout-step ${state}`} data-testid={testId} data-look={state}>
      <div className="poc-rollout-step-head">
        <strong>{t(STEP_LABELS[key])}</strong>
        <span className="poc-meta" data-testid={`${testId}-look`}>{t(LOOK_KEYS[state])}</span>
        {key === "PocSourceCommit" && view.sourceCommitSha && (
          <span className="mono" data-testid={`${testId}-sha`}>{view.sourceCommitSha.slice(0, 7)}</span>
        )}
        {key === "PocPipelineBuild" && view.pipelineUrl && (
          <a href={view.pipelineUrl} target="_blank" rel="noreferrer" data-testid={`${testId}-pipeline`}>{t("pocs.revision.pipeline")} ↗</a>
        )}
        {key === "GitOpsCommit" && view.gitopsCommitSha && (view.gitopsCommitUrl
          ? <a href={view.gitopsCommitUrl} target="_blank" rel="noreferrer" className="mono" data-testid={`${testId}-commit`}>
              {view.gitopsCommitSha.slice(0, 7)} ↗</a>
          : <span className="mono" data-testid={`${testId}-commit`}>{view.gitopsCommitSha.slice(0, 7)}</span>)}
      </div>
      {key === "PocPipelineBuild" && revision && <PipelineJobs revision={revision} testId={`${testId}-jobs`} />}
      {row.resultMessage && <div className="poc-meta" data-testid={`${testId}-result`}>{row.resultMessage}</div>}
      {reached && (lines.length > 0 ? (
        <ul className="poc-rollout-lines" data-testid={`${testId}-lines`}>
          {lines.map((line) => (
            <li key={line.key} className={`tone-${line.tone}`}>
              {line.at && <span className="mono poc-meta">{line.at}</span>} {line.text}
            </li>
          ))}
        </ul>
      ) : row.status === "running" && <div className="poc-meta">{t("pocs.rollout.noLines")}</div>)}
    </li>
  );
}

// 2026-11-23-933dd: the host pipeline's build, image scan and push, marked from the revision as its page marks them.
function PipelineJobs({ revision, testId }: { revision: PocRevisionView; testId: string }) {
  const t = useT();
  const jobs = revisionSteps(revision).filter((step) => PIPELINE_JOBS.has(step.id));
  return (
    <ol className="poc-strip" data-testid={testId}>
      {jobs.map((job) => (
        <li key={job.id} className={`poc-chip ${job.look}`} data-testid={`${testId}-${job.id}`} data-look={job.look}>
          {t(job.label)}
        </li>
      ))}
    </ol>
  );
}
