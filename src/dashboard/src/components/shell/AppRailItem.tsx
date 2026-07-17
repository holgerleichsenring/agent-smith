"use client";

import Link from "next/link";
import { cn } from "@/lib/utils";

// p0209a: one row of the persistent left app rail. Prop-driven so it has no
// knowledge of routing or activity sources — the parent (AppRail) derives
// `active` from the current route and supplies live/freshness from whatever
// is readily available.
// p0343c (pixel identity): emits the ratified mock's .nav DOM verbatim —
// .ni icon slot · label · .nc count/freshness pill (.nc.hot for attention).
// When no glyph icon is supplied (SYSTEM subsystem rows), the .ni slot carries
// the live/idle dot so subsystem liveness stays visible.

export interface AppRailItemProps {
  label: string;
  href: string;
  live?: boolean;
  freshness?: string | null;
  active: boolean;
  /** Live count rendered as the mock's .nc pill. Omitted = no count. */
  count?: number;
  /** Attention state — the count pill goes .nc.hot (amber). */
  hot?: boolean;
  /** Kept for API compatibility; the mock rail has no indent tier. */
  indent?: boolean;
  /** The mock's .ni glyph (◉ ? ▶ ≡ ✓ ◈ …). Falls back to the live/idle dot. */
  icon?: string;
}

export function AppRailItem(props: AppRailItemProps) {
  const fresh = props.freshness && props.freshness !== "—" ? props.freshness : null;
  return (
    <Link
      href={props.href}
      data-testid={`app-rail-item-${props.label}`}
      data-active={props.active ? "true" : "false"}
      data-hot={props.hot ? "true" : "false"}
      aria-current={props.active ? "page" : undefined}
      className={cn("nav", props.active && "on")}
    >
      <span className="ni">
        {props.icon ?? (
          <span
            data-testid="app-rail-item-dot"
            className={cn(
              "inline-block h-2 w-2 rounded-full align-middle",
              props.hot
                ? "bg-[var(--run)]"
                : props.live
                ? "bg-[var(--ok)]"
                : "bg-[var(--idle)]",
            )}
            aria-label={props.hot ? "needs attention" : props.live ? "live" : "idle"}
          />
        )}
      </span>
      {props.label}
      {props.count !== undefined && (
        <span
          data-testid={`app-rail-count-${props.label}`}
          className={cn("nc", props.hot && "hot")}
        >
          {props.count}
        </span>
      )}
      {fresh && <span className="nc">{fresh}</span>}
    </Link>
  );
}
