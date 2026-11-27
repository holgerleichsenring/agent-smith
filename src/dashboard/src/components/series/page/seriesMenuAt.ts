// 2026-11-27-ecf2a: where a ticket's details open in the viewport — under a graph node (over it near the window's foot), or
// beside a row of the rail's list, where the graph is (under the row when the window has no room beside it).

const MENU_W = 260;
/** Below an anchor with less room than this under it, the menu opens above it. */
const MENU_ROOM = 320;
const GAP = 6;
const EDGE = 8;

/** Where the menu sits in the viewport. */
export type MenuAt = { left: number; top: number } | { left: number; bottom: number };

/** Under the anchor, or over it when the anchor is near the window's foot. */
export function menuBelow(own: DOMRect): MenuAt {
  const left = Math.max(EDGE, Math.min(own.left, window.innerWidth - MENU_W - EDGE));
  return window.innerHeight - own.bottom >= MENU_ROOM || own.top < MENU_ROOM
    ? { left, top: own.bottom + GAP } : { left, bottom: window.innerHeight - own.top + GAP };
}

/** To the right of the anchor, its top level with the anchor's while the window has the room; else as menuBelow. */
export function menuBeside(own: DOMRect): MenuAt {
  if (window.innerWidth - own.right < MENU_W + 2 * EDGE) return menuBelow(own);
  const top = Math.max(EDGE, Math.min(own.top, window.innerHeight - MENU_ROOM));
  return { left: own.right + EDGE, top };
}
