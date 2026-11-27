"use client";

import { useT } from "@/hooks/useT";
import type { SeriesRunDetail } from "@/types/seriesRuns";

// 2026-11-27-3532e: the master's question is its plain chat message; when the series waits on it and the master gave
// choices, they are small quick-reply buttons under the message list, the recommended one in the accent and named so for a
// screen reader. A click sends the choice as the conversation's next message, which the series takes as the answer.
// SeriesQuestionBlock decides when they are drawn (runs.control, a conversation that takes a message).

interface Props {
  detail: SeriesRunDetail;
  /** Sends the choice as the conversation's next message. */
  onAnswer: (answer: string) => void;
}

export function SeriesQuickReplies({ detail, onAnswer }: Props) {
  const t = useT();
  return (
    <div className="sr-quick" role="group" aria-label={t("series.chat.quickReplies")} data-testid="series-quick-replies">
      {detail.questionChoices.map((label, index) => (
        <button key={index} type="button" className={index === detail.questionRecommended ? "btn primary" : "btn"}
          data-testid={`series-quick-reply-${label}`} onClick={() => onAnswer(label)}>
          {label}
          {index === detail.questionRecommended && <span className="sr-only"> ({t("dialog.question.recommended")})</span>}
        </button>
      ))}
    </div>
  );
}
