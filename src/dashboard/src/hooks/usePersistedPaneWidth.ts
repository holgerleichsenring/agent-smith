"use client";

import { useCallback, useEffect, useState } from "react";

// p0395: one persisted pane dimension for the resizable trace drawer. Null
// means "the stylesheet default" — nothing is written until the operator
// actually drags. The stored value is read in an effect (not the initial
// state) so the server-rendered markup and the first client render agree.
//
// p0395a: what persists is a FRACTION of a caller-supplied basis (the
// viewport for the drawer's own width, the drawer for the rail split), so a
// wider window widens the pane proportionally with no user action. The hook
// returns pixels: the fraction re-derived against the current basis and
// clamped to the caller's px bounds on every render — the basis changing on
// a window resize is what makes the panes fluid.
//
// Storage discriminator (same keys as p0395): p0395 stored absolute PIXELS,
// always > 1; this format stores fractions, always <= 1. A stored value > 1
// is therefore a legacy pixel width — it is interpreted against the basis at
// load time and re-stored as a fraction, preserving the operator's dragged
// size instead of resetting it.

export function usePersistedPaneWidth(
  storageKey: string,
  basisPx: number | null,
  minPx: number,
  maxPx: number,
): [number | null, (px: number) => void] {
  const [fraction, setFraction] = useState<number | null>(null);

  // Loading (and legacy migration) needs a real basis — wait for one.
  useEffect(() => {
    if (basisPx == null || basisPx <= 0) {
      return;
    }
    setFraction(readFraction(storageKey, basisPx));
  }, [storageKey, basisPx]);

  const set = useCallback(
    (px: number) => {
      if (basisPx == null || basisPx <= 0) {
        return;
      }
      const next = roundFraction(Math.min(1, px / basisPx));
      setFraction(next);
      write(storageKey, next);
    },
    [storageKey, basisPx],
  );

  const px =
    fraction == null || basisPx == null || basisPx <= 0
      ? null
      : clamp(Math.round(fraction * basisPx), minPx, maxPx);
  return [px, set];
}

function readFraction(key: string, basisPx: number): number | null {
  const raw = readNumber(key);
  if (raw == null) {
    return null;
  }
  if (raw <= 1) {
    return raw;
  }
  // Legacy absolute pixels — interpret against the current basis and re-store.
  const migrated = roundFraction(Math.min(1, raw / basisPx));
  write(key, migrated);
  return migrated;
}

function readNumber(key: string): number | null {
  try {
    const parsed = Number.parseFloat(window.localStorage.getItem(key) ?? "");
    return Number.isFinite(parsed) && parsed > 0 ? parsed : null;
  } catch {
    return null;
  }
}

function write(key: string, fraction: number): void {
  try {
    window.localStorage.setItem(key, String(fraction));
  } catch {
    /* storage unavailable — the size still applies for this session */
  }
}

function roundFraction(fraction: number): number {
  return Math.round(fraction * 10000) / 10000;
}

function clamp(value: number, min: number, max: number): number {
  return Math.min(max, Math.max(min, value));
}
