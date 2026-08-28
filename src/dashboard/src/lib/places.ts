import { ENTITY_KINDS, ENTITY_LABEL } from "@/components/config/entities";
import { SETTING_KEYS, SETTING_LABEL } from "@/components/config/settings";

// 2026-08-27-1ed6: the header says WHERE YOU ARE, and this is the one table it reads.
// A second list of names beside the rail's would drift on the first rename, so the rail
// links and the header labels are asserted against each other by a test rather than kept
// in step by hand.
//
// The table maps a PATH to a place, not a rail entry to a place: five monitor entries
// share "/" and differ only by a ?bucket= query, which the root layout may not read (a
// search-param hook there fails the build for every statically rendered route). So "/" is
// ONE place — "Runs" — and a query string is dropped before the lookup.
//
// The catalog and settings places are DERIVED from the same constants the config rail and
// the studio render, so a renamed entity kind or settings key renames its place too.

const RUN_PLACE = "Run";
const RUN_WHY_PLACE = "Run · why";

const FIXED_PLACES: Record<string, string> = {
  "/": "Runs",
  "/pull-requests": "Pull requests",
  // 2026-08-27-7463: the one Insight destination — spend, runs by outcome and
  // criteria outcomes, where /system/cost, /system/today and /system/expectations
  // used to be three pages.
  "/overview": "Overview",
  "/identity": "Your identity",
  "/system": "System",
  "/system/tracker": "Tracker",
  "/system/webhooks": "Webhooks",
  "/system/chat": "Chat dispatchers",
  "/system/config": "Config file reads",
  "/system/catalog": "Skill catalog & vocabulary",
  // The configuration subtree. A bare /config serves the first catalog kind, so it is
  // that kind's place — the header names what is on screen, not the area it belongs to.
  "/config": ENTITY_LABEL[ENTITY_KINDS[0]],
  "/config/access": "Permissions",
  "/config/changes": "Changes",
  "/config/installation": "Installation",
  "/config/connection-check": "Connection check",
  "/config/settings": SETTING_LABEL[SETTING_KEYS[0]],
};

function catalogPlaces(): Record<string, string> {
  return Object.fromEntries(ENTITY_KINDS.map((kind) => [`/config/${kind}`, ENTITY_LABEL[kind]]));
}

function settingsPlaces(): Record<string, string> {
  return Object.fromEntries(
    SETTING_KEYS.map((key) => [`/config/settings/${key}`, SETTING_LABEL[key]]),
  );
}

/** Every path the header can name, and what it names it. */
export const PLACES: Readonly<Record<string, string>> = Object.freeze({
  ...FIXED_PLACES,
  ...catalogPlaces(),
  ...settingsPlaces(),
});

/**
 * The place a path is, or null where the table names none — a header that guesses is a
 * header that lies the first time a route is added without one.
 */
export function placeForPath(pathname: string | null | undefined): string | null {
  const path = normalisePath(pathname);
  return PLACES[path] ?? runPlace(path);
}

// A run's id is minted per run, so the two run views are the one pair of places the table
// cannot list by path.
function runPlace(path: string): string | null {
  const segments = path.split("/").filter((segment) => segment.length > 0);
  if (segments[0] !== "jobs" || segments.length < 2) return null;
  if (segments.length === 2) return RUN_PLACE;
  return segments.length === 3 && segments[2] === "why" ? RUN_WHY_PLACE : null;
}

// A query is a view of a place, never a place of its own; a trailing slash is the same
// path written differently.
function normalisePath(pathname: string | null | undefined): string {
  const path = (pathname ?? "/").split("?")[0].split("#")[0];
  if (path.length > 1 && path.endsWith("/")) return path.slice(0, -1);
  return path.length > 0 ? path : "/";
}
