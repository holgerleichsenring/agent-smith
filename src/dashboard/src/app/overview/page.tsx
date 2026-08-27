import { OverviewView } from "@/components/overview/OverviewView";

// 2026-08-27-7463: the Overview — the one Insight destination, replacing the
// Cost, Today's activity and Expectations rollups. The body lives in the view so
// the page exports only its default, satisfying Next's Page-type contract.
export default function OverviewPage() {
  return <OverviewView />;
}
