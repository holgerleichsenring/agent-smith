"use client";

import { useEffect, useState } from "react";
import { fetchRunTraceEntry, type RunTraceEntryHeader } from "@/lib/runStoryApi";
import { useRunTrace } from "@/hooks/useRunTrace";
import { formatChars } from "./callSeries";
import { cn } from "@/lib/utils";
import { useDateLocale, useT } from "@/hooks/useT";
import type { T, TextKey } from "@/lib/i18n/translate";

// p0423b: a traced run's conversation, readable entry by entry in call order — what was
// asked, what came back, and what the tools returned AS THE MODEL RECEIVED IT. A run that
// was not traced has no entries, and the reader is then ABSENT, never broken: an empty
// reader promising content that was never recorded is worse than no reader at all.
//
// One entry is fetched at a time. The list carries sizes only, because a recorded prompt
// reaches megabytes and nobody asked to download the run to see that it exists.

const LABEL_TITLE: Record<string, TextKey> = {
  prompt: "runs.why.trace.label.prompt",
  answer: "runs.why.trace.label.answer",
  tool: "runs.why.trace.label.tool",
};

function labelTitle(t: T, label: string): string {
  const key = LABEL_TITLE[label];
  return key ? t(key) : label;
}

export function TraceReader({ runId }: { runId: string }) {
  const t = useT();
  const locale = useDateLocale();
  const { entries, loading } = useRunTrace(runId);
  const [selected, setSelected] = useState<RunTraceEntryHeader | null>(null);

  if (loading) return <p className="hint">{t("runs.why.trace.looking")}</p>;
  if (entries.length === 0) {
    return (
      <p className="hint" data-testid="trace-reader-absent">
        {t("runs.why.trace.absent")}
      </p>
    );
  }

  return (
    <div className="trace-grid" data-testid="trace-reader" data-entries={entries.length}>
      <nav className="nav" aria-label={t("runs.why.trace.nav")}>
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
            {labelTitle(t, entry.label)}
            <small style={{ color: "var(--ink-3)" }}> · {formatChars(entry.chars, locale)}</small>
          </button>
        ))}
      </nav>
      <TraceEntryBody runId={runId} entry={selected} />
    </div>
  );
}

function TraceEntryBody({ runId, entry }: { runId: string; entry: RunTraceEntryHeader | null }) {
  const t = useT();
  const locale = useDateLocale();
  const [content, setContent] = useState<string | null>(null);
  const [error, setError] = useState<Error | null>(null);

  useEffect(() => {
    setContent(null);
    setError(null);
    if (!entry) return;
    const ctrl = new AbortController();
    fetchRunTraceEntry(runId, entry.sequence, entry.label, ctrl.signal)
      .then(setContent)
      .catch((err: unknown) => {
        if (!ctrl.signal.aborted) setError(err instanceof Error ? err : new Error(String(err)));
      });
    return () => ctrl.abort();
  }, [runId, entry]);

  if (!entry) {
    return (
      <p className="hint" data-testid="trace-entry-none">
        {t("runs.why.trace.pick")}
      </p>
    );
  }
  if (error) return <p className="hint">{t("runs.why.trace.failed", { message: error.message })}</p>;

  return (
    <div data-testid="trace-entry-body" data-sequence={entry.sequence}>
      <div className="kv" style={{ marginBottom: 8 }}>
        <span className="mono">{entry.sequence.toString().padStart(4, "0")}</span>{" "}
        {labelTitle(t, entry.label)} · {t("runs.why.trace.characters", { chars: formatChars(entry.chars, locale) })}
      </div>
      <pre
        className="mono"
        style={{
          whiteSpace: "pre-wrap", wordBreak: "break-word", maxHeight: 520, overflow: "auto",
          background: "var(--panel)", border: "1px solid var(--line)", borderRadius: "var(--r-s)",
          padding: 12, fontSize: "12px", margin: 0,
        }}
      >
        {content ?? t("runs.why.trace.loading")}
      </pre>
    </div>
  );
}
