"use client";

import Link from "next/link";
import { RELEASE_VERSION } from "@/lib/buildIdentity";

// 2026-08-27-729e: one line, on the one surface that is on every route — which build this
// is. NOT in the banner stack: those name what is WRONG, and a permanent panel among them
// becomes furniture, which is the reasoning RailIdentity already applied above it.
//
// It reads the release THIS BUNDLE was stamped with, so it is true without a request and
// survives a server that is not answering. It is labelled as the dashboard's own for the
// same reason; the server's and the sandbox agent's are one click away, where they can be
// read side by side.

export function RailRelease() {
  return (
    <div className="border-t border-[var(--line)] px-3 py-2" data-testid="rail-release">
      <Link href="/system/installation" className="block text-xs" data-testid="rail-release-link">
        {RELEASE_VERSION ? `Dashboard ${RELEASE_VERSION}` : "Installation"}
      </Link>
    </div>
  );
}
