"use client";

import Link from "next/link";
import { usePathname } from "next/navigation";
import { placeForPath } from "@/lib/places";
import { HeaderIdentity } from "./HeaderIdentity";

// 2026-08-27-1ed6: the header on every route. It carries the current place on the left,
// and on the right the two things that belong to no single page: the way into
// configuration and who is signed in.
//
// It sits in the layout's first grid ROW rather than inside <main>: main is the scroll
// container, so a header inside it scrolls away, and a header above the grid would push
// the full-height rail off the bottom by exactly its own height.

export function AppHeader() {
  const pathname = usePathname();
  const place = placeForPath(pathname);
  return (
    <header data-testid="app-header" className="mock-shell topbar col-span-2">
      {place && (
        <div className="tb-place" data-testid="app-header-place">
          {place}
        </div>
      )}
      <div className="tb-actions">
        <Link
          href="/config"
          data-testid="app-header-gear"
          aria-label="Configuration"
          title="Configuration"
          className="tb-btn tb-icon"
        >
          <GearGlyph />
        </Link>
        <HeaderIdentity />
      </div>
    </header>
  );
}

// The conventional gear, drawn: a hub, a ring and eight teeth on the ring's normals. A
// text ⚙ renders as whatever emoji font the operator's machine happens to carry, at a
// size and colour the page does not control.
function GearGlyph() {
  return (
    <svg
      width="16"
      height="16"
      viewBox="0 0 16 16"
      fill="none"
      stroke="currentColor"
      strokeWidth="1.4"
      strokeLinecap="round"
      strokeLinejoin="round"
      aria-hidden="true"
    >
      <circle cx="8" cy="8" r="2.3" />
      <path d="M8 1.2v2M8 12.8v2M1.2 8h2M12.8 8h2M3.2 3.2l1.4 1.4M11.4 11.4l1.4 1.4M12.8 3.2l-1.4 1.4M4.6 11.4l-1.4 1.4" />
      <circle cx="8" cy="8" r="5.6" />
    </svg>
  );
}
