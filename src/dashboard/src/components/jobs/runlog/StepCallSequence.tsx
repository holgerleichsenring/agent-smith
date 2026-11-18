"use client";

import { useEffect, useMemo, useState } from "react";
import { CallPrompt } from "@/components/jobs/why/CallPrompt";
import { fetchPromptDigest, type PromptDigestView } from "@/lib/runStoryApi";
import type { RunEvent } from "@/types/hub-events";
import { callSequence, spendWords, tokenWords, type SequenceCall } from "./callSequence";

// 2026-11-18-e06be: an open step's model calls, one line each — number, role · model, tokens, cost and time — with what
// the call was fed and what it decided as two dim lines under it. A click opens the call's whole prompt in place (the
// Why page's CallPrompt). The newest 20 show; "show earlier" adds 20 more, reading older events once the page runs out.

export const SEQUENCE_PAGE = 20;

interface Props {
  runId: string;
  events: RunEvent[];
  /** The step's llm call count, for numbering when older events are not loaded. */
  total: number | null;
  hasOlder: boolean;
  loading: boolean;
  loadOlder: () => void;
  runEnded: boolean;
}

export function StepCallSequence({ runId, events, total, hasOlder, loading, loadOlder, runEnded }: Props) {
  const calls = useMemo(() => callSequence(events, total, !hasOlder), [events, total, hasOlder]);
  const [shown, setShown] = useState(SEQUENCE_PAGE);
  const [open, setOpen] = useState<string | null>(null);
  const visible = calls.slice(Math.max(0, calls.length - shown));
  const digest = useDigest(runId, visible);
  const hidden = Math.max(0, calls.length - shown);
  const showEarlier = () => {
    if (hidden === 0 && hasOlder) loadOlder();
    setShown((s) => s + SEQUENCE_PAGE);
  };

  if (calls.length === 0) {
    return <div className="rl-detail-line rl-dim" data-testid="step-calls-empty">{loading ? "reading the step…" : "no model call recorded here"}</div>;
  }
  return (
    <div className="rl-calls" data-testid="step-calls">
      {(hidden > 0 || hasOlder) && (
        <button type="button" className="rl-filter rl-more-lines" disabled={loading} onClick={showEarlier} data-testid="step-calls-earlier">
          {loading ? "loading…" : hidden > 0 ? `show earlier (${hidden} more)` : "show earlier"}
        </button>
      )}
      {visible.map((c) => (
        <Call key={c.key} call={c} runId={runId} runEnded={runEnded} said={c.promptId ? digest[c.promptId] : undefined}
          open={open === c.key} onToggle={() => setOpen(open === c.key ? null : c.key)} />
      ))}
    </div>
  );
}

function Call({ call, runId, runEnded, said, open, onToggle }: {
  call: SequenceCall; runId: string; runEnded: boolean; said: PromptDigestView | undefined; open: boolean; onToggle: () => void;
}) {
  const fed = said?.fed ?? call.fed;
  const decided = said?.decided ?? call.decided;
  return (
    <div className="rl-call" data-testid="step-call" data-n={call.n ?? ""}>
      <button type="button" className="rl-call-line" aria-expanded={open} disabled={!call.promptId} onClick={onToggle}
        title={call.promptId ? "open the prompt" : "this call's prompt was not stored"}>
        <span className="rl-call-n">#{call.n ?? "?"}</span>
        <span className="rl-call-what">{call.activity} · {call.model}</span>
        <span className="rl-call-tokens">{tokenWords(call)}</span>
        <span className="rl-call-spend">{spendWords(call, runEnded)}</span>
      </button>
      {fed && <div className="rl-call-io" data-testid="step-call-fed" title={fed}>← {fed}</div>}
      {decided && <div className="rl-call-io" data-testid="step-call-decided" title={decided}>→ {decided}</div>}
      {open && call.promptId && (
        <div className="rl-call-prompt"><CallPrompt runId={runId} promptId={call.promptId} index={call.n ?? 0} /></div>
      )}
    </div>
  );
}

/** The digest lines of the shown calls, read once per call; a failed read leaves the event-derived lines. */
function useDigest(runId: string, visible: SequenceCall[]): Record<string, PromptDigestView> {
  const [said, setSaid] = useState<Record<string, PromptDigestView>>({});
  const key = visible.filter((c) => c.finished && c.promptId && !(c.promptId in said)).map((c) => c.promptId).join(",");
  useEffect(() => {
    if (!key) return;
    const ctrl = new AbortController();
    const ids = key.split(",");
    fetchPromptDigest(runId, ids, ctrl.signal)
      .then((calls) => setSaid((s) => ({
        ...s,
        // An id the store does not hold is remembered as empty, so it is not asked for again.
        ...Object.fromEntries(ids.map((id) => [id, { promptId: id, fed: null, decided: null }])),
        ...Object.fromEntries(calls.map((c) => [c.promptId, c])),
      })))
      .catch(() => { /* the event-derived lines stay */ });
    return () => ctrl.abort();
  }, [runId, key]);
  return said;
}
