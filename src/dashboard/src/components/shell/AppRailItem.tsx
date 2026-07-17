"use client";

import Link from "next/link";

// p0209a: one row of the persistent left app rail. Prop-driven so it has no
// knowledge of routing or activity sources — the parent (AppRail) derives
// `active` from the current route and supplies live/freshness from whatever
// is readily available. live/idle dot · label · optional freshness label.
// Mirrors the .item / .idot / .ilbl / .ifresh shapes in the p0209 mockup.
// p0345b: optional `count` (live bucket size) + `hot` (attention: amber) +
// `indent` (monitor sub-item under Runs) for the mission-control sub-sections.

export interface AppRailItemProps {
  label: string;
  href: string;
  live?: boolean;
  freshness?: string | null;
  active: boolean;
  /** Live count rendered at the right edge. Omitted = no count badge. */
  count?: number;
  /** Attention state — amber dot + amber count (Needs-you > 0). */
  hot?: boolean;
  /** Renders as an indented sub-item (monitor sections under Runs). */
  indent?: boolean;
}

export function AppRailItem(props: AppRailItemProps) {
  const selectedCls = props.active
    ? "bg-emerald-50 border-l-emerald-500"
    : "border-l-transparent hover:bg-stone-50";
  const labelTone = props.active
    ? "font-semibold text-emerald-700"
    : props.hot
    ? "font-semibold text-amber-700"
    : "text-stone-700";
  const dotTone = props.hot
    ? "bg-amber-500 animate-pulse"
    : props.live
    ? "bg-emerald-500"
    : "bg-stone-300";
  const fresh = props.freshness && props.freshness !== "—" ? props.freshness : null;
  return (
    <Link
      href={props.href}
      data-testid={`app-rail-item-${props.label}`}
      data-active={props.active ? "true" : "false"}
      data-hot={props.hot ? "true" : "false"}
      aria-current={props.active ? "page" : undefined}
      className={`flex select-none items-center gap-2.5 border-l-[3px] py-2.5 pr-5 dsh-body ${props.indent ? "pl-9" : "pl-5"} ${selectedCls}`}
    >
      <span
        data-testid="app-rail-item-dot"
        className={`h-2 w-2 flex-none rounded-full ${dotTone}`}
        aria-label={props.hot ? "needs attention" : props.live ? "live" : "idle"}
      />
      <span className={`flex-1 truncate ${labelTone}`}>{props.label}</span>
      {props.count !== undefined && (
        <span
          data-testid={`app-rail-count-${props.label}`}
          className={`flex-none font-mono dsh-mono ${
            props.hot ? "font-semibold text-amber-700" : props.count > 0 ? "text-stone-500" : "text-stone-300"
          }`}
        >
          {props.count}
        </span>
      )}
      {fresh && (
        <span className="flex-none font-mono dsh-mono text-stone-400">{fresh}</span>
      )}
    </Link>
  );
}
