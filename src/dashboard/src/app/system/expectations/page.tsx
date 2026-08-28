import { redirect } from "next/navigation";

// 2026-08-27-7463: the Expectations rollup is the Overview's criteria-outcomes
// section. The old path still reaches the same measurements.
export default function ExpectationsRollupRedirect() {
  redirect("/overview");
}
