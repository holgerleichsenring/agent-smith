import { describe, it, expect, vi, beforeEach } from "vitest";
import CostRollupRedirect from "@/app/system/cost/page";
import TodayRollupRedirect from "@/app/system/today/page";
import ExpectationsRollupRedirect from "@/app/system/expectations/page";

// 2026-08-27-7463: the three rollup pages became three sections of one Overview.
// The old paths are declared as route redirects rather than a branch inside the
// view: /system is a client optional-catch-all, so a redirect decided in the view
// would render the old page AND the redirect. This is the mechanism 2026-08-27-1ed6
// used to move /system/installation and /system/connections.

const redirect = vi.hoisted(() => vi.fn());
vi.mock("next/navigation", () => ({ redirect }));

beforeEach(() => redirect.mockReset());

describe("Overview route", () => {
  it("Route_TheOldRollupPaths_LandOnTheOverview", () => {
    CostRollupRedirect();
    TodayRollupRedirect();
    ExpectationsRollupRedirect();
    expect(redirect.mock.calls).toEqual([["/overview"], ["/overview"], ["/overview"]]);
  });
});
