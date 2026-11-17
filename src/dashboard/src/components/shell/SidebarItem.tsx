"use client";

import Link from "next/link";
import type { LucideIcon } from "lucide-react";
import { cn } from "@/lib/utils";

// 2026-11-16-0440c: one row of the flat sidebar — an icon, the label and a muted count on the
// right. Collapsed, only the icon shows; the label stays the link's accessible name and title,
// and an attention count becomes a dot on the icon.

export interface SidebarItemProps {
  label: string;
  href: string;
  icon: LucideIcon;
  active: boolean;
  collapsed: boolean;
  /** Omitted while unknown, never a made-up 0. */
  count?: number;
  /** The count asks for an answer: it turns amber, and collapsed it is a dot. */
  attention?: boolean;
  /** Lets an item act on the page it is already on; the href stays a real destination. */
  onClick?: (event: React.MouseEvent<HTMLAnchorElement>) => void;
  testId: string;
}

export function SidebarItem({ label, href, icon: Icon, active, collapsed, count, attention, onClick, testId }: SidebarItemProps) {
  const hot = attention === true && (count ?? 0) > 0;
  return (
    <Link
      href={href}
      title={label}
      aria-label={collapsed ? label : undefined}
      aria-current={active ? "page" : undefined}
      onClick={onClick}
      className={cn("sb-item", active && "on")}
      data-testid={testId}
    >
      <Icon size={17} strokeWidth={1.75} aria-hidden="true" className="sb-ic" />
      {!collapsed && <span className="sb-label">{label}</span>}
      {!collapsed && count !== undefined && (
        <span className={cn("sb-count", hot && "hot")} data-testid={`${testId}-count`}>
          {count}
        </span>
      )}
      {collapsed && hot && <span className="sb-dot" data-testid={`${testId}-dot`} aria-hidden="true" />}
    </Link>
  );
}

export function SidebarGroup({ label, collapsed }: { label: string; collapsed: boolean }) {
  return collapsed ? (
    <div className="sb-rule" aria-hidden="true" />
  ) : (
    <div className="sb-group" data-testid={`sidebar-group-${label}`}>
      {label}
    </div>
  );
}
