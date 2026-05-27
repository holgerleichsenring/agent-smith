"use client";

import type { LlmCallStartedEvent, LlmCallFinishedEvent } from "@/types/hub-events";

interface Props {
  started: LlmCallStartedEvent;
  finished: LlmCallFinishedEvent;
}

export function LlmCallPayload({ started, finished }: Props) {
  const totalTokens = finished.tokensIn + finished.tokensOut;
  return (
    <div className="space-y-2 text-sm" data-testid="llm-call-payload">
      <header className="flex items-baseline justify-between gap-3">
        <h3 className="font-medium text-stone-800">{started.role}</h3>
        <span className="text-xs text-stone-500">{started.model}</span>
      </header>
      <dl className="grid grid-cols-2 gap-x-4 gap-y-1 text-xs">
        <dt className="text-stone-500">Tokens in</dt>
        <dd className="font-mono text-stone-800">{finished.tokensIn.toLocaleString()}</dd>
        <dt className="text-stone-500">Tokens out</dt>
        <dd className="font-mono text-stone-800">{finished.tokensOut.toLocaleString()}</dd>
        <dt className="text-stone-500">Total</dt>
        <dd className="font-mono text-stone-800">{totalTokens.toLocaleString()}</dd>
        <dt className="text-stone-500">Cost (USD)</dt>
        <dd className="font-mono text-stone-800">${finished.costUsd.toFixed(4)}</dd>
        <dt className="text-stone-500">Duration</dt>
        <dd className="font-mono text-stone-800">{finished.durationMs}ms</dd>
        <dt className="text-stone-500">Prompt hash</dt>
        <dd className="font-mono text-stone-800">{started.promptHash}</dd>
      </dl>
      <p className="text-[10px] text-stone-400">
        Prompt content stays in result.md per p0169e — the hash is for correlation only.
      </p>
    </div>
  );
}
