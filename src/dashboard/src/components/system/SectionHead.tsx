import type { ReactNode } from "react";

// 2026-08-27-7463: the head a SECTION opens with, as the parity mocks draw it
// (.section-head — a rule, an h2, an optional count pill and a right-aligned
// caption). A page has one PageHead and as many of these below it as it has
// readings; a section rendering its own h1 is what made three thin pages
// uncomposable in the first place.

export function SectionHead({
  title,
  count,
  sub,
}: {
  title: string;
  count?: number;
  sub?: ReactNode;
}) {
  return (
    <div className="section-head">
      <h2>{title}</h2>
      {count !== undefined && <span className="cnt">{count}</span>}
      {sub && <span className="sh-sub">{sub}</span>}
    </div>
  );
}
