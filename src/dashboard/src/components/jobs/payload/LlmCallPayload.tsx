"use client";

import { useDateLocale, useT } from "@/hooks/useT";

import type { LlmCallStartedEvent, LlmCallFinishedEvent } from "@/types/hub-events";

interface Props {
  started: LlmCallStartedEvent;
  finished: LlmCallFinishedEvent;
}

export function LlmCallPayload({ started, finished }: Props) {
  const t = useT();
  const locale = useDateLocale();
  const totalTokens = finished.tokensIn + finished.tokensOut;
  return (
    <div className="space-y-2 text-sm" data-testid="llm-call-payload">
      <header className="flex items-baseline justify-between gap-3">
        <h3 className="font-medium text-stone-800">{started.role}</h3>
        <span className="text-xs text-stone-500">{started.model}</span>
      </header>
      <dl className="grid grid-cols-2 gap-x-4 gap-y-1 text-xs">
        <dt className="text-stone-500">{t("runs.payload.tokensIn")}</dt>
        <dd className="font-mono text-stone-800">{finished.tokensIn.toLocaleString(locale)}</dd>
        <dt className="text-stone-500">{t("runs.payload.tokensOut")}</dt>
        <dd className="font-mono text-stone-800">{finished.tokensOut.toLocaleString(locale)}</dd>
        <dt className="text-stone-500">{t("runs.payload.total")}</dt>
        <dd className="font-mono text-stone-800">{totalTokens.toLocaleString(locale)}</dd>
        <dt className="text-stone-500">{t("runs.payload.cost")}</dt>
        <dd className="font-mono text-stone-800">${finished.costUsd.toFixed(4)}</dd>
        <dt className="text-stone-500">{t("runs.payload.duration")}</dt>
        <dd className="font-mono text-stone-800">{finished.durationMs}ms</dd>
        <dt className="text-stone-500">{t("runs.payload.promptHash")}</dt>
        <dd className="font-mono text-stone-800">{started.promptHash}</dd>
      </dl>
      <p className="dsh-label text-stone-400">
        {t("runs.payload.promptNote")}
      </p>
    </div>
  );
}
