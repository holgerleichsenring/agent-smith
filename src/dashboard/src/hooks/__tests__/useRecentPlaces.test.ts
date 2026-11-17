import { renderHook } from "@testing-library/react";
import { beforeEach, describe, expect, it } from "vitest";
import { recentPlaceFor, remember, useRecentPlaces, type RecentPlace } from "../useRecentPlaces";

// 2026-11-16-0440c: the pages this browser opened last, for the sidebar's Recent group.

const place = (n: number): RecentPlace => ({ href: `/jobs/r-${n}`, label: `Run ${n}`, kind: "run" });

// This jsdom build ships no localStorage; a Map stands in for it.
function stubStorage(): void {
  const backing = new Map<string, string>();
  Object.defineProperty(window, "localStorage", {
    configurable: true,
    value: {
      getItem: (k: string) => backing.get(k) ?? null,
      setItem: (k: string, v: string) => void backing.set(k, v),
      removeItem: (k: string) => void backing.delete(k),
      clear: () => backing.clear(),
    },
  });
}

beforeEach(stubStorage);

describe("useRecentPlaces", () => {
  it("RecentPlaceFor_ATicketAndARun_AreRecent_OtherPagesAreNot", () => {
    expect(recentPlaceFor("/tickets/my%20shop/GH-101")).toEqual({ href: "/tickets/my%20shop/GH-101", label: "GH-101 · my shop", kind: "ticket" });
    expect(recentPlaceFor("/jobs/2026-08-17T21-30-46-a98c")).toEqual({ href: "/jobs/2026-08-17T21-30-46-a98c", label: "Run a98c", kind: "run" });
    expect(recentPlaceFor("/jobs/r-1/why")).toBeNull();
    expect(recentPlaceFor("/tickets")).toBeNull();
    expect(recentPlaceFor("/overview")).toBeNull();
  });

  it("Remember_PutsThePlaceFirst_DropsItsEarlierVisit_KeepsFive", () => {
    const five = [1, 2, 3, 4, 5].map(place);
    expect(remember(five, place(3)).map((p) => p.href)).toEqual(["/jobs/r-3", "/jobs/r-1", "/jobs/r-2", "/jobs/r-4", "/jobs/r-5"]);
    expect(remember(five, place(6))).toHaveLength(5);
  });

  it("UseRecentPlaces_RemembersAcrossMounts", () => {
    renderHook(() => useRecentPlaces("/jobs/r-1")).unmount();
    const { result } = renderHook(() => useRecentPlaces("/overview"));
    expect(result.current.map((p) => p.href)).toEqual(["/jobs/r-1"]);
  });

  it("UseRecentPlaces_BlockedStorage_KeepsThisPageOnly_NotACrash", () => {
    Object.defineProperty(window, "localStorage", { configurable: true, get: () => { throw new Error("blocked"); } });
    const { result } = renderHook(() => useRecentPlaces("/jobs/r-1"));
    expect(result.current.map((p) => p.href)).toEqual(["/jobs/r-1"]);
  });
});
