"use client";

import { useCallback, useState } from "react";
import type { PendingQuestionInfo } from "@/types/hub-events";

// p0327: the answer affordance for a waiting_for_input run. Renders the parked
// run's pending question (text, context, choices, timeout default) and posts
// the operator's answer to /api/runs/{runId}/answer — the durable inbox +
// resume sweeper take it from there; the run then continues as the SAME run.
// p0343c (pixel identity): emits the mock's .q-item quick-reply DOM (question
// text, meta line, .q-opts choice buttons) plus the .n-answer free-text row
// with the green "Send & resume run" button. Used inline on the home screen's
// .need cards AND inside the run viewer's dialogue drawer.

const API_BASE = process.env.NEXT_PUBLIC_API_BASE_URL ?? "";

interface Props {
  runId: string;
  question: PendingQuestionInfo;
}

export function PendingQuestionCard({ runId, question }: Props) {
  const [text, setText] = useState("");
  const [sent, setSent] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const submit = useCallback(async (answer: string) => {
    if (!answer.trim() || sent) return;
    setError(null);
    try {
      const res = await fetch(`${API_BASE}/api/runs/${encodeURIComponent(runId)}/answer`, {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ answer: answer.trim() }),
      });
      if (res.ok || res.status === 202) setSent(true);
      else setError(`HTTP ${res.status}`);
    } catch (err) {
      setError(err instanceof Error ? err.message : "request failed");
    }
  }, [runId, sent]);

  const quick = question.type === "Approval" ? ["approve", "reject"] : question.choices;

  return (
    <div data-testid="pending-question-card">
      <div className="q-item">
        <div className="qt">{question.text}</div>
        {question.context && <div className="qm">{question.context}</div>}
        {question.defaultAnswer && !sent && (
          <div className="qm">
            No answer by {new Date(question.answerDeadlineAt).toLocaleString()} applies the
            default: “{question.defaultAnswer}”.
          </div>
        )}
        {!sent && quick.length > 0 && (
          <div className="q-opts">
            {quick.map((label) => (
              <button
                key={label}
                type="button"
                className="q-opt"
                data-testid={`pending-question-choice-${label}`}
                onClick={() => submit(label)}
              >
                {label}
              </button>
            ))}
          </div>
        )}
        {sent && (
          <div className="qm" data-testid="pending-question-sent">
            Answer sent — the run resumes shortly.
          </div>
        )}
        {error && (
          <div className="qm" style={{ color: "var(--bad)" }}>
            {error}
          </div>
        )}
      </div>
      {!sent && (
        <div className="n-answer" style={{ marginTop: 12 }}>
          <input
            type="text"
            value={text}
            onChange={(e) => setText(e.target.value)}
            onKeyDown={(e) => {
              if (e.key === "Enter") submit(text);
            }}
            placeholder="Add context for the agent (optional)"
            data-testid="pending-question-input"
          />
          <button
            type="button"
            className="btn primary"
            data-testid="pending-question-submit"
            onClick={() => submit(text)}
          >
            Send &amp; resume run
          </button>
        </div>
      )}
    </div>
  );
}
