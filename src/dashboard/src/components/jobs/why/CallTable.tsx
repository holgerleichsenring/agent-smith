"use client";

import { useState } from "react";
import type { RunCallPoint } from "@/lib/runStoryApi";
import { CallPrompt } from "./CallPrompt";
import { formatChars, formatMs } from "./callSeries";

// p0423b: the plot's table view. It exists for two reasons and both are required: a chart
// whose series colour sits under 3:1 against the surface owes the reader a labelled
// alternative, and a shape that looks wrong has to be turned back into the exact numbers
// somebody can quote in a defect report.

const CELL: React.CSSProperties = { padding: "4px 8px", textAlign: "right", whiteSpace: "nowrap" };
const OPEN: React.CSSProperties = {
  font: "inherit", background: "none", border: 0, padding: 0, color: "var(--ink)",
  textDecoration: "underline", textUnderlineOffset: 2, cursor: "pointer",
};
const HEAD: React.CSSProperties = { ...CELL, color: "var(--ink-3)", fontWeight: 600 };

// 2026-11-09-0620a: a call whose prompt was stored opens it below the table from its prompt cell.
export function CallTable({ runId, calls }: { runId?: string; calls: RunCallPoint[] }) {
  const [open, setOpen] = useState<RunCallPoint | null>(null);
  if (calls.length === 0) return null;
  return (
    <>
    <div style={{ maxHeight: 260, overflow: "auto" }} data-testid="call-table">
      <table style={{ width: "100%", borderCollapse: "collapse", fontSize: "12px" }}>
        <caption className="hint" style={{ captionSide: "top", textAlign: "left" }}>
          Every model call of this spec, in call order.
        </caption>
        <thead>
          <tr>
            <th style={{ ...HEAD, textAlign: "left" }} scope="col">Call</th>
            <th style={HEAD} scope="col">Prompt</th>
            <th style={HEAD} scope="col">Tools</th>
            <th style={HEAD} scope="col">Cached</th>
            <th style={HEAD} scope="col">Answer</th>
            <th style={HEAD} scope="col">Took</th>
            <th style={HEAD} scope="col">Throttled</th>
            <th style={{ ...HEAD, textAlign: "left" }} scope="col">Outcome</th>
          </tr>
        </thead>
        <tbody>
          {calls.map((call) => (
            <tr
              key={call.index}
              data-testid="call-row"
              data-outcome={call.outcome}
              style={{ borderTop: "1px solid var(--line-2)" }}
            >
              <th scope="row" style={{ ...CELL, textAlign: "left", fontWeight: 400 }} className="mono">
                {call.index}
                {call.attempt > 1 && (
                  <small style={{ color: "var(--ink-3)" }}> · attempt {call.attempt}</small>
                )}
              </th>
              <td style={CELL} className="mono">
                {runId && call.promptId ? (
                  <button
                    type="button"
                    className="mono"
                    style={OPEN}
                    data-testid="call-prompt-open"
                    aria-expanded={open?.index === call.index}
                    onClick={() => setOpen(open?.index === call.index ? null : call)}
                  >
                    {formatChars(call.promptChars)}
                  </button>
                ) : formatChars(call.promptChars)}
              </td>
              <td style={CELL} className="mono">{call.toolCount ? call.toolCount : "—"}</td>
              <td style={CELL} className="mono">{cachedShare(call)}</td>
              <td style={CELL} className="mono">{formatChars(call.answerChars)}</td>
              <td style={CELL} className="mono">{formatMs(call.durationMs)}</td>
              <td style={CELL} className="mono">
                {call.throttleWaitMs > 0 ? formatMs(call.throttleWaitMs) : "—"}
              </td>
              <td style={{ ...CELL, textAlign: "left" }}>
                <span className={call.outcome === "Ok" ? "badge neu" : "badge bad"}>
                  {call.outcome.toLowerCase()}
                </span>
              </td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
    {runId && open?.promptId && <CallPrompt runId={runId} promptId={open.promptId} index={open.index} />}
    </>
  );
}

// The share of the prompt the provider served from its cache. OpenAI-style providers count the cached tokens inside
// tokensIn; the server emits the uncached remainder, so the total is the two summed.
function cachedShare(call: RunCallPoint): string {
  const cached = call.cachedTokensIn ?? 0;
  const total = (call.tokensIn ?? 0) + cached;
  return total > 0 && cached > 0 ? `${Math.round((cached / total) * 100)}%` : "—";
}
