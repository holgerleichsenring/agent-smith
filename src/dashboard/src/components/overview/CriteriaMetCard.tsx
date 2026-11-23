import type { ExpectationRead } from "@/hooks/useExpectationMetrics";
import { judgedSentence, percentOrDash } from "@/lib/expectationTotals";
import { OverviewCard } from "@/components/overview/OverviewCard";
import { useT } from "@/hooks/useT";
import type { T } from "@/lib/i18n/translate";

// The share of criteria finished coding runs met, operator overrules applied, as one
// figure with its proportion drawn beneath it. It reads the same counts as the panel
// below it, so the two cannot disagree.

interface CriteriaReading {
  value: string;
  detail: string;
  share?: number;
}

export function CriteriaMetCard({ read }: { read: ExpectationRead }) {
  const t = useT();
  const reading = criteriaReading(t, read);
  return (
    <OverviewCard
      label={t("overview.criteria.label")}
      value={reading.value}
      share={reading.share}
      detail={reading.detail}
      testId="overview-criteria-card"
    />
  );
}

// A share never renders as 0% without a measurement: an unread, failed or unjudged
// installation gets a dash and a sentence, not a number.
function criteriaReading(t: T, { data, error }: ExpectationRead): CriteriaReading {
  if (error) return { value: "—", detail: t("overview.criteria.unavailable") };
  if (!data) return { value: "—", detail: t("overview.criteria.reading") };
  const { counts } = data;
  if (counts.share === null) {
    return { value: "—", detail: `${t("overview.criteria.runs", { count: data.runs })} · ${t("overview.criteria.noneJudged")}` };
  }
  return {
    value: percentOrDash(counts.share),
    share: counts.share,
    detail: `${judgedSentence(counts)} · ${t("overview.criteria.runs", { count: data.runs })}`,
  };
}

