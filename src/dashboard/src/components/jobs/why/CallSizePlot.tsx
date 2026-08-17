"use client";

import { useState } from "react";
import type { RunCallPoint } from "@/lib/runStoryApi";
import { formatChars, formatMs, niceMax } from "./callSeries";
import { CallSizePanel } from "./CallSizePanel";

// p0423b: the centrepiece of the story view. In the run that named the wall, the prompt
// grew 151k -> 216k -> 278k -> 341k -> 357k while the answer shrank 3,886 -> 2,750 -> 969
// -> 0 and the last call hung for 23 minutes. Nobody reads that out of a table of numbers;
// everybody sees it as a shape. So it is drawn — prompt size against answer size, per call,
// in call order — and it is drawn BEFORE the last call stalls, not after.
//
// The two measures never share a y-axis: two panels, one call order.

const PROMPT_COLOR = "var(--run)";
const PROMPT_WASH = "var(--run-wash)";
const ANSWER_COLOR = "var(--accent)";
const ANSWER_WASH = "var(--accent-wash)";

export function CallSizePlot({ calls }: { calls: RunCallPoint[] }) {
  const [hovered, setHovered] = useState<number | null>(null);

  if (calls.length === 0) {
    return (
      <p className="hint" data-testid="call-size-plot-empty">
        No model call was recorded for this phase.
      </p>
    );
  }

  const prompts = calls.map((c) => c.promptChars);
  const answers = calls.map((c) => c.answerChars);
  const silent = calls
    .map((c, position) => (c.answerChars === 0 ? position : -1))
    .filter((position) => position >= 0);
  const focus = calls[hovered ?? calls.length - 1];

  return (
    <div data-testid="call-size-plot" data-calls={calls.length}>
      <Readout call={focus} pinned={hovered == null} />
      <CallSizePanel
        title="Prompt sent"
        values={prompts}
        color={PROMPT_COLOR}
        wash={PROMPT_WASH}
        max={niceMax(Math.max(...prompts))}
        hovered={hovered}
        onHover={setHovered}
        testId="call-size-prompt"
      />
      <CallSizePanel
        title="Answer returned"
        values={answers}
        color={ANSWER_COLOR}
        wash={ANSWER_WASH}
        max={niceMax(Math.max(...answers))}
        marked={silent}
        hovered={hovered}
        onHover={setHovered}
        testId="call-size-answer"
      />
      <div
        style={{
          display: "flex", justifyContent: "space-between",
          fontSize: "11px", color: "var(--ink-3)", marginTop: 2,
        }}
      >
        <span>call {calls[0].index}</span>
        <span>call order →</span>
        <span>call {calls[calls.length - 1].index}</span>
      </div>
      {silent.length > 0 && (
        <p className="hint" data-testid="call-size-silent">
          {silent.length === 1 ? "One call" : `${silent.length} calls`} returned nothing —
          marked on the answer panel.
        </p>
      )}
    </div>
  );
}

// The shared tooltip: one row naming the call both panels are showing. It reads the LAST
// call until the operator hovers, so the plot arrives already pointing at the run's end.
function Readout({ call, pinned }: { call: RunCallPoint; pinned: boolean }) {
  return (
    <div
      className="kv"
      data-testid="call-size-readout"
      data-call={call.index}
      style={{ fontSize: "12px", color: "var(--ink-2)", marginBottom: 6 }}
    >
      <span className="mono">
        call {call.index}
        {pinned ? " (latest)" : ""}
      </span>
      {" · "}
      <span>prompt {formatChars(call.promptChars)}</span>
      {" · "}
      <span>answer {formatChars(call.answerChars)}</span>
      {" · "}
      <span>{formatMs(call.durationMs)}</span>
      {call.role && <span> · {call.role}</span>}
      {call.outcome !== "Ok" && (
        <span className="badge bad" style={{ marginLeft: 6 }}>
          {call.outcome.toLowerCase()}
        </span>
      )}
    </div>
  );
}
