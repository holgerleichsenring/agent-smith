import type { ExpectationRead } from "@/hooks/useExpectationMetrics";
import { judgedSentence, percentOrDash } from "@/lib/expectationTotals";
import { OverviewCard } from "@/components/overview/OverviewCard";

// The share of criteria finished coding runs met, operator overrules applied, as one
// figure with its proportion drawn beneath it. It reads the same counts as the panel
// below it, so the two cannot disagree.

interface CriteriaReading {
  value: string;
  detail: string;
  share?: number;
}

export function CriteriaMetCard({ read }: { read: ExpectationRead }) {
  const reading = criteriaReading(read);
  return (
    <OverviewCard
      label="Criteria met"
      value={reading.value}
      share={reading.share}
      detail={reading.detail}
      testId="overview-criteria-card"
    />
  );
}

// A share never renders as 0% without a measurement: an unread, failed or unjudged
// installation gets a dash and a sentence, not a number.
function criteriaReading({ data, error }: ExpectationRead): CriteriaReading {
  if (error) return { value: "—", detail: "Criteria unavailable" };
  if (!data) return { value: "—", detail: "Reading judged criteria…" };
  const { counts } = data;
  if (counts.share === null) {
    return { value: "—", detail: `${runsPhrase(data.runs)} · no criterion judged yet` };
  }
  return {
    value: percentOrDash(counts.share),
    share: counts.share,
    detail: `${judgedSentence(counts)} · ${runsPhrase(data.runs)}`,
  };
}

function runsPhrase(runs: number): string {
  return `${runs} coding run${runs === 1 ? "" : "s"}`;
}
