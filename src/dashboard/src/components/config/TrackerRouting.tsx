"use client";

import type { StudioTracker } from "@/lib/configApi";

// 2026-09-16-74a2: the label map belongs to the TRACKER, so the project's pipeline
// section renders it read-only and says where it is edited. The sentence underneath is
// the one the form never carried: the project's own default pipeline does NOT decide
// what an incoming ticket runs, and a form that implied it did was wrong in as many
// words. What decides is PipelineResolver over the merged trigger —
//   a label map with a match  -> that pipeline
//   a label map with no match -> ProjectResolver drops the ticket, the default is never read
//   no label map at all       -> the trigger's default, or the undeclared fallback
// so each of those three states gets its own sentence rather than one hedge.

/** Kept in step with PipelinePresets.UndeclaredFallbackPipeline (2026-09-16-a4d7). */
const UNDECLARED_FALLBACK = "fix-bug";

export function TrackerRouting({
  tracker,
  trackerId,
  testId = "form-tracker-routing",
}: {
  tracker: StudioTracker | undefined;
  trackerId: string;
  testId?: string;
}) {
  const labels = Object.entries(tracker?.pipelineFromLabel ?? {});
  return (
    <div className="field" data-testid={testId}>
      <label>
        how a ticket reaches a pipeline
        <span className="help">
          {tracker ? `owned by tracker ${trackerId} — edit it there` : "pick a tracker first"}
        </span>
      </label>

      {!tracker && (
        <span className="help" data-testid={`${testId}-no-tracker`}>
          this project names no tracker yet, so nothing routes to it
        </span>
      )}

      {tracker && labels.length > 0 && (
        <div className="maprows" data-testid={`${testId}-map`}>
          {labels.map(([label, pipeline]) => (
            <div className="tpl-target" key={label} data-testid={`${testId}-row-${label}`}>
              {label} → {pipeline}
            </div>
          ))}
        </div>
      )}

      {tracker && (
        <span className="help" data-testid={`${testId}-fallback`}>
          {labels.length > 0
            ? "a ticket whose label matches none of these is not routed to this project at all."
            : fallbackSentence(tracker)}
        </span>
      )}
    </div>
  );
}

function fallbackSentence(tracker: StudioTracker): string {
  const declared = tracker.defaultPipeline;
  return declared
    ? `this tracker maps no label, so every ticket it routes here runs ${declared} — what this tracker declares.`
    : `this tracker maps no label and declares no default, so every ticket it routes here runs ${UNDECLARED_FALLBACK} — nothing declares that; set a default pipeline on the tracker.`;
}
