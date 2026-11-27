"use client";

import { useT } from "@/hooks/useT";
import { Markdown } from "@/components/ui/Markdown";
import type { SeriesRunDetail } from "@/types/seriesRuns";

// 2026-11-27-3532e: the series' waiting question where no quick replies are drawn, without a box. Over an open chat it is a
// one-line pointer (the message itself is in the chat), so the status bar's anchor never lands on an empty block; where no
// chat is drawn (a gone or closed conversation) it is the question in full, with its choices and the recommended one.

export function SeriesQuestionLine({ detail, full }: { detail: SeriesRunDetail; full: boolean }) {
  const t = useT();
  const text = detail.question ?? "";
  if (!full) {
    const first = text.split(/\r?\n/).find((line) => line.trim().length > 0) ?? text;
    return <p className="sr-question-line sr-question-pointer" data-testid="series-question-pointer">{first}</p>;
  }
  const choices = detail.questionChoices.map((label, index) =>
    index === detail.questionRecommended ? `${label} (${t("dialog.question.recommended")})` : label);
  return (
    <div className="sr-question-line" data-testid="series-question-text">
      <Markdown>{text}</Markdown>
      {choices.length > 0 && <p data-testid="series-question-choices">{t("series.chat.choices", { choices: choices.join(" / ") })}</p>}
    </div>
  );
}
