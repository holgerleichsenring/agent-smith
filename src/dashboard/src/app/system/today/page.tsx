import { redirect } from "next/navigation";

// 2026-08-27-7463: the Today's-activity rollup is gone — every one of its six
// numbers renders on the subsystem page it describes (Tracker, Webhooks). Its
// path leads to the Overview, which is where the readings that had nowhere else
// to go now live.
export default function TodayRollupRedirect() {
  redirect("/overview");
}
