import type { ExpectationRead } from "@/hooks/useExpectationMetrics";
import { ratifiedCount, sumOutcomeCounts } from "@/lib/expectationTotals";
import { OverviewCard } from "@/components/overview/OverviewCard";

// 2026-08-27-559e: how often a draft was ratified verbatim, as one figure with
// its proportion drawn beneath it. It is the panel's own hit rate — the same
// sum over the same per-project counts — so the card and the panel below it
// cannot disagree.

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

// A rate never renders as 0% without a measurement: an unread, failed, empty or
// wholly unratified installation gets a dash and a sentence, not a number.
function criteriaReading({ data, error }: ExpectationRead): CriteriaReading {
  if (error) return { value: "—", detail: "Criteria outcomes unavailable" };
  if (!data) return { value: "—", detail: "Reading ratification outcomes…" };
  const sum = sumOutcomeCounts(data.projects);
  const ratified = ratifiedCount(sum);
  if (ratified <= 0) {
    return { value: "—", detail: `${sum.total} negotiated · none ratified yet` };
  }
  const share = sum.verbatim / ratified;
  return {
    value: `${Math.round(share * 100)}%`,
    share,
    detail: `${sum.verbatim} of ${ratified} ratified criteria verified`,
  };
}
