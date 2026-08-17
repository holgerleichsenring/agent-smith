"use client";

import { useEffect, useState } from "react";
import { fetchRunTraceEntry, type RunTraceEntryHeader } from "@/lib/runStoryApi";
import { useRunTrace } from "@/hooks/useRunTrace";
import { formatChars } from "./callSeries";
import { cn } from "@/lib/utils";

// p0423b: a traced run's conversation, readable entry by entry in call order — what was
// asked, what came back, and what the tools returned AS THE MODEL RECEIVED IT. A run that
// was not traced has no entries, and the reader is then ABSENT, never broken: an empty
// reader promising content that was never recorded is worse than no reader at all.
//
// One entry is fetched at a time. The list carries sizes only, because a recorded prompt
// reaches megabytes and nobody asked to download the run to see that it exists.

const LABEL_TITLE: Record<string, string> = {
  prompt: "Prompt",
  answer: "Answer",
  tool: "Tool result",
};

export function TraceReader({ runId }: { runId: string }) {
  const { entries, loading } = useRunTrace(runId);
  const [selected, setSelected] = useState<RunTraceEntryHeader | null>(null);

  if (loading) return <p className="hint">Looking for a recorded conversation…</p>;
  if (entries.length === 0) {
    return (
      <p className="hint" data-testid="trace-reader-absent">
        This run was not traced, so there is no conversation to read. Runs record their
        numbers always; the conversation is recorded on a switch.
      </p>
    );
  }

  return (
    <div className="trace-grid" data-testid="trace-reader" data-entries={entries.length}>
      <nav className="nav" aria-label="Recorded conversation">
        {entries.map((entry) => (
          <button
            key={`${entry.sequence}.${entry.label}`}
            type="button"
            data-testid="trace-entry"
            data-sequence={entry.sequence}
            className={cn("li", selected?.sequence === entry.sequence
              && selected.label === entry.label && "on")}
            onClick={() => setSelected(entry)}
          >
            <span className="mono">{entry.sequence.toString().padStart(4, "0")}</span>{" "}
            {LABEL_TITLE[entry.label] ?? entry.label}
            <small style={{ color: "var(--ink-3)" }}> · {formatChars(entry.chars)}</small>
          </button>
        ))}
      </nav>
      <TraceEntryBody runId={runId} entry={selected} />
    </div>
  );
}

function TraceEntryBody({ runId, entry }: { runId: string; entry: RunTraceEntryHeader | null }) {
  const [content, setContent] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    setContent(null);
    setError(null);
    if (!entry) return;
    const ctrl = new AbortController();
    fetchRunTraceEntry(runId, entry.sequence, entry.label, ctrl.signal)
      .then(setContent)
      .catch((err: unknown) => {
        if (!ctrl.signal.aborted) setError(err instanceof Error ? err.message : String(err));
      });
    return () => ctrl.abort();
  }, [runId, entry]);

  if (!entry) {
    return (
      <p className="hint" data-testid="trace-entry-none">
        Pick an entry to read what the model saw at that point in the run.
      </p>
    );
  }
  if (error) return <p className="hint">Could not read that entry: {error}</p>;

  return (
    <div data-testid="trace-entry-body" data-sequence={entry.sequence}>
      <div className="kv" style={{ marginBottom: 8 }}>
        <span className="mono">{entry.sequence.toString().padStart(4, "0")}</span>{" "}
        {LABEL_TITLE[entry.label] ?? entry.label} · {formatChars(entry.chars)} characters
      </div>
      <pre
        className="mono"
        style={{
          whiteSpace: "pre-wrap", wordBreak: "break-word", maxHeight: 520, overflow: "auto",
          background: "var(--panel)", border: "1px solid var(--line)", borderRadius: "var(--r-s)",
          padding: 12, fontSize: "12px", margin: 0,
        }}
      >
        {content ?? "Loading…"}
      </pre>
    </div>
  );
}
