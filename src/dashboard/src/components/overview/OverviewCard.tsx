import type { ReactNode } from "react";

// 2026-08-27-559e: the Overview's card — a small uppercase label, one large
// figure, an optional proportion bar and one line of detail. Three of them sit
// in a row above the two panels, so the eye lands on one number per card and
// the three compare; the strip they replace spread seven equal cells across the
// page and read as a rail count repeated.

export function OverviewCard({
  label,
  value,
  detail,
  share,
  testId,
}: {
  label: string;
  value: ReactNode;
  detail: ReactNode;
  /** Drawn as the card's proportion bar, 0..1. Omitted when the figure has no share. */
  share?: number;
  testId: string;
}) {
  return (
    <div className="ov-card" data-testid={testId}>
      <span className="k">{label}</span>
      <span className="v">{value}</span>
      {share !== undefined && (
        <span className="meter" aria-hidden>
          <span style={{ width: `${Math.round(share * 100)}%` }} />
        </span>
      )}
      <span className="d">{detail}</span>
    </div>
  );
}
