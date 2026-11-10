"use client";

import { useEffect, useState } from "react";
import { fetchCallPrompt, type PromptRecordView } from "@/lib/runStoryApi";
import { formatChars } from "./callSeries";

// 2026-11-09-0620a: what one model call was sent, in send order — the tool list, the system prompt and every
// message — so a prompt can be read instead of reconstructed from code. One block per row; a block's content
// opens on demand, because a tool list or a read file runs to tens of thousands of characters.

export function CallPrompt({ runId, promptId, index }: { runId: string; promptId: string; index: number }) {
  const [view, setView] = useState<PromptRecordView | null>(null);
  const [error, setError] = useState<Error | null>(null);

  useEffect(() => {
    setView(null);
    setError(null);
    const ctrl = new AbortController();
    fetchCallPrompt(runId, promptId, ctrl.signal)
      .then(setView)
      .catch((err: unknown) => {
        if (!ctrl.signal.aborted) setError(err instanceof Error ? err : new Error(String(err)));
      });
    return () => ctrl.abort();
  }, [runId, promptId]);

  if (error) return <p className="hint">Could not read the prompt of call {index}: {error.message}</p>;
  if (!view) return <p className="hint">Loading the prompt of call {index}…</p>;

  return (
    <div data-testid="call-prompt" data-prompt={view.id} style={{ marginTop: 8 }}>
      <div className="kv" style={{ marginBottom: 6 }}>
        Call {index} was sent {view.blocks.length} blocks and offered {view.toolCount} tools.
        {!view.complete && " Its earliest blocks were removed by retention."}
      </div>
      {view.blocks.map((block) => (
        <details key={block.position} data-testid="call-prompt-block" data-kind={block.kind} data-role={block.role}>
          <summary className="mono" style={{ fontSize: "12px", cursor: "pointer" }}>
            {block.position} · {block.kind === "Tools" ? "tools" : block.role} · {formatChars(block.chars)}
          </summary>
          <pre className="mono" style={PRE}>{block.content}</pre>
        </details>
      ))}
    </div>
  );
}

const PRE: React.CSSProperties = {
  whiteSpace: "pre-wrap", wordBreak: "break-word", maxHeight: 420, overflow: "auto",
  background: "var(--panel)", border: "1px solid var(--line)", borderRadius: "var(--r-s)",
  padding: 12, fontSize: "12px", margin: "4px 0 8px",
};
