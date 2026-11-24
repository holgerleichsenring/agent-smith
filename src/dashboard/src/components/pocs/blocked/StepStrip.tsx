"use client";

import { useT } from "@/hooks/useT";
import type { TextKey } from "@/lib/i18n/translate";
import { blockingVerdict } from "@/lib/pocBlockingVerdict";
import type { PocRevisionView } from "@/lib/pocsApi";
import { cn } from "@/lib/utils";

// 2026-11-23-933dc: the way a revision travels — upload checked, code scan, commit, then the image steps a later spec
// adds. Each step reads done, blocked or pending from the revision alone; nothing here guesses at a step not reached.

export type StepLook = "done" | "blocked" | "pending";

export interface RevisionStep {
  id: string;
  label: TextKey;
  look: StepLook;
}

// 2026-11-23-933dd: the host pipeline's three jobs — all done once the revision is built; blocked at the image scan with
// the build behind it when the image stage blocks.
function imageSteps(view: PocRevisionView): RevisionStep[] {
  const built = view.state === "built";
  const imageBlocked = view.state === "blocked" && view.blockedStage === "image";
  return [
    { id: "build", label: "pocs.revision.step.build", look: built || imageBlocked ? "done" : "pending" },
    { id: "imageScan", label: "pocs.revision.step.imageScan", look: built ? "done" : imageBlocked ? "blocked" : "pending" },
    { id: "push", label: "pocs.revision.step.push", look: built ? "done" : "pending" },
    { id: "deploy", label: "pocs.revision.step.deploy", look: "pending" },
  ];
}

/** The steps of a revision and how far it got. */
export function revisionSteps(view: PocRevisionView): RevisionStep[] {
  const upload: StepLook = view.state === "admitted" || view.state === "refused" ? "pending" : "done";
  const scanned = view.verdict !== null;
  const codeBlocked = view.state === "blocked" && view.blockedStage !== "image";
  const scan: StepLook = codeBlocked ? "blocked"
    : view.state === "overruled" || (scanned && !view.verdict?.isBlocked) || view.commitSha ? "done" : "pending";
  const commit: StepLook = view.commitSha ? "done" : "pending";
  return [
    { id: "upload", label: "pocs.revision.step.upload", look: upload },
    { id: "codeScan", label: "pocs.revision.step.codeScan", look: scan },
    { id: "commit", label: "pocs.revision.step.commit", look: commit },
    ...imageSteps(view),
  ];
}

export function StepStrip({ view }: { view: PocRevisionView }) {
  const t = useT();
  const blocks = blockingVerdict(view)?.blocks.length ?? 0;
  return (
    <ol className="poc-strip" aria-label={t("pocs.revision.steps")} data-testid="poc-revision-steps">
      {revisionSteps(view).map((step) => (
        <li key={step.id} className={cn("poc-chip", step.look)} data-testid={`poc-step-${step.id}`} data-look={step.look}>
          {step.look === "done" && <Mark path="M3 8.5l3 3 7-7" />}
          {step.look === "blocked" && <Mark path="M4 4l8 8M12 4l-8 8" />}
          {t(step.label)}
          {(step.id === "codeScan" || step.id === "imageScan") && step.look === "blocked" && blocks > 0 && (
            <span> · {t("pocs.revision.step.blocks", { count: blocks })}</span>
          )}
          {step.id === "codeScan" && view.state === "overruled" && <span> · {t("pocs.revision.step.overruled")}</span>}
          {step.id === "commit" && view.commitSha && <span className="mono">{view.commitSha.slice(0, 7)}</span>}
          {step.id === "push" && view.digest && <span className="mono" data-testid="poc-step-digest">{view.digest.slice(7, 19)}</span>}
        </li>
      ))}
    </ol>
  );
}

function Mark({ path }: { path: string }) {
  return (
    <svg width="13" height="13" viewBox="0 0 16 16" fill="none" stroke="currentColor" strokeWidth="2.4" aria-hidden="true">
      <path d={path} />
    </svg>
  );
}
