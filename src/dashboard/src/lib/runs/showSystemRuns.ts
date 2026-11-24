// 2026-11-24-35ff: whether the runs list shows the runs the server starts itself, per browser. Off by default. One value
// for the whole page, so the list and the sidebar's Runs count change together. Blocked storage keeps the choice for this
// page only.

const KEY = "agentsmith.show-system-runs";

let current: boolean | null = null;
const listeners = new Set<() => void>();

function stored(): boolean {
  try {
    return window.localStorage.getItem(KEY) === "1";
  } catch {
    return false;
  }
}

export function currentShowSystemRuns(): boolean {
  current ??= stored();
  return current;
}

export function chooseShowSystemRuns(show: boolean): void {
  current = show;
  try {
    window.localStorage.setItem(KEY, show ? "1" : "0");
  } catch {
    /* kept for this page only */
  }
  listeners.forEach((listener) => listener());
}

export function subscribeShowSystemRuns(listener: () => void): () => void {
  listeners.add(listener);
  return () => listeners.delete(listener);
}

/** Tests: re-read storage on the next read. */
export function resetShowSystemRuns(): void {
  current = null;
  listeners.forEach((listener) => listener());
}
