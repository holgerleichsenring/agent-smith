"use client";

import { useCallback, useState } from "react";
import { MessageCircleQuestion } from "lucide-react";
import type { PendingQuestionInfo } from "@/types/hub-events";

// p0327: the answer affordance for a waiting_for_input run. Renders the parked
// run's pending question (text, context, choices, timeout default) and posts
// the operator's answer to /api/runs/{runId}/answer — the durable inbox +
// resume sweeper take it from there; the run then continues as the SAME run.

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

  return (
    <div
      data-testid="pending-question-card"
      className="mt-3 rounded-lg border border-violet-200 bg-violet-50 px-4 py-3 text-sm text-violet-950"
    >
      <div className="flex items-start gap-3">
        <MessageCircleQuestion aria-hidden="true" className="mt-0.5 h-4 w-4 flex-none text-violet-500" />
        <div className="min-w-0 flex-1 space-y-2">
          <div className="font-medium">{question.text}</div>
          {question.context && (
            <pre className="max-h-48 overflow-auto whitespace-pre-wrap rounded bg-white/60 px-3 py-2 text-xs text-violet-900">
              {question.context}
            </pre>
          )}
          {sent ? (
            <div data-testid="pending-question-sent" className="text-violet-700">
              Answer sent — the run resumes shortly.
            </div>
          ) : (
            <AnswerControls
              question={question}
              text={text}
              onText={setText}
              onSubmit={submit}
            />
          )}
          {error && <div className="text-rose-600">{error}</div>}
          {question.defaultAnswer && !sent && (
            <div className="text-xs text-violet-500">
              No answer by {new Date(question.answerDeadlineAt).toLocaleString()} applies
              the default: “{question.defaultAnswer}”.
            </div>
          )}
        </div>
      </div>
    </div>
  );
}

function AnswerControls({
  question,
  text,
  onText,
  onSubmit,
}: {
  question: PendingQuestionInfo;
  text: string;
  onText: (v: string) => void;
  onSubmit: (answer: string) => void;
}) {
  const quick = question.type === "Approval" ? ["approve", "reject"] : question.choices;
  return (
    <div className="space-y-2">
      {quick.length > 0 && (
        <div className="flex flex-wrap gap-2">
          {quick.map((label) => (
            <button
              key={label}
              type="button"
              data-testid={`pending-question-choice-${label}`}
              onClick={() => onSubmit(label)}
              className="rounded border border-violet-300 bg-white px-2.5 py-1 text-xs font-medium text-violet-700 transition hover:bg-violet-100"
            >
              {label}
            </button>
          ))}
        </div>
      )}
      <div className="flex gap-2">
        <input
          type="text"
          value={text}
          onChange={(e) => onText(e.target.value)}
          onKeyDown={(e) => { if (e.key === "Enter") onSubmit(text); }}
          placeholder="Type an answer…"
          data-testid="pending-question-input"
          className="min-w-0 flex-1 rounded border border-violet-200 bg-white px-2.5 py-1 text-sm text-stone-800 outline-none focus:border-violet-400"
        />
        <button
          type="button"
          data-testid="pending-question-submit"
          onClick={() => onSubmit(text)}
          className="rounded bg-violet-600 px-3 py-1 text-xs font-medium text-white transition hover:bg-violet-700"
        >
          Answer
        </button>
      </div>
    </div>
  );
}
