import { redirect } from "next/navigation";

// 2026-08-27-7463: the Cost rollup is a section of the Overview. A bookmark and a
// link in a ticket still land on the figures they named.
export default function CostRollupRedirect() {
  redirect("/overview");
}
