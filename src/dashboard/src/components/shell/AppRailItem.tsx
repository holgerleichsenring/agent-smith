"use client";

import Link from "next/link";

// p0209a: one row of the persistent left app rail. Prop-driven so it has no
// knowledge of routing or activity sources — the parent (AppRail) derives
// `active` from the current route and supplies live/freshness from whatever
// is readily available. live/idle dot · label · optional freshness label.
// Mirrors the .item / .idot / .ilbl / .ifresh shapes in the p0209 mockup.

export interface AppRailItemProps {
  label: string;
  href: string;
  live?: boolean;
  freshness?: string | null;
  active: boolean;
}

export function AppRailItem(props: AppRailItemProps) {
  const selectedCls = props.active
    ? "bg-emerald-50 border-l-emerald-500"
    : "border-l-transparent hover:bg-stone-50";
  const labelTone = props.active ? "font-semibold text-emerald-700" : "text-stone-700";
  const dotTone = props.live ? "bg-emerald-500" : "bg-stone-300";
  const fresh = props.freshness && props.freshness !== "—" ? props.freshness : null;
  return (
    <Link
      href={props.href}
      data-testid={`app-rail-item-${props.label}`}
      data-active={props.active ? "true" : "false"}
      aria-current={props.active ? "page" : undefined}
      className={`flex select-none items-center gap-2.5 border-l-[3px] px-5 py-2.5 text-[14px] ${selectedCls}`}
    >
      <span
        data-testid="app-rail-item-dot"
        className={`h-2 w-2 flex-none rounded-full ${dotTone}`}
        aria-label={props.live ? "live" : "idle"}
      />
      <span className={`flex-1 truncate ${labelTone}`}>{props.label}</span>
      {fresh && (
        <span className="flex-none font-mono text-[11.5px] text-stone-400">{fresh}</span>
      )}
    </Link>
  );
}
