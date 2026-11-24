"use client";

import { useT } from "@/hooks/useT";

import { EventType, type RunEvent, type StepFinishedEvent, type StepStartedEvent } from "@/types/hub-events";

export function StepPayload({ events }: { events: RunEvent[] }) {
  const t = useT();
  const started = events.find((e) => e.type === EventType.StepStarted) as StepStartedEvent | undefined;
  const finished = events.find((e) => e.type === EventType.StepFinished) as StepFinishedEvent | undefined;
  return (
    <div className="space-y-2 text-sm" data-testid="step-payload">
      {started && (
        <header>
          <h3 className="font-medium text-stone-800">{started.stepName}</h3>
          <p className="text-xs text-stone-500">
            {t("runs.step.of", { index: started.stepIndex, total: started.totalSteps })}
          </p>
        </header>
      )}
      {finished && (
        <dl className="grid grid-cols-2 gap-x-4 gap-y-1 text-xs">
          <dt className="text-stone-500">{t("runs.payload.status")}</dt>
          <dd className="font-mono text-stone-800">{finished.status}</dd>
          <dt className="text-stone-500">{t("runs.payload.duration")}</dt>
          <dd className="font-mono text-stone-800">{finished.durationMs}ms</dd>
        </dl>
      )}
    </div>
  );
}
