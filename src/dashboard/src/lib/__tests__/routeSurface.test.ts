import { describe, it, expect } from "vitest";
import { translator } from "@/lib/i18n/translate";
import { routeSurfaceName as keyOf } from "../routeSurface";

const routeSurfaceName = (path: string | null) => translator("en")(keyOf(path));

// 2026-08-25-39ab: the route boundary names what the operator was looking at,
// not the URL they were at.

describe("routeSurfaceName", () => {
  it("routeSurfaceName_TheRoot_IsTheHomePage", () => {
    expect(routeSurfaceName("/")).toBe("home page");
  });

  it("routeSurfaceName_ARunUrl_IsTheRunView", () => {
    expect(routeSurfaceName("/jobs/9f3c-1b2a/why")).toBe("run view");
  });

  it("routeSurfaceName_EachKnownSection_HasItsOwnName", () => {
    expect(routeSurfaceName("/config/agents")).toBe("configuration");
    expect(routeSurfaceName("/system/health")).toBe("system view");
    expect(routeSurfaceName("/pull-requests")).toBe("pull requests");
  });

  it("routeSurfaceName_ARouteItDoesNotKnow_StillReadsAsASentence", () => {
    expect(routeSurfaceName("/something-new")).toBe("page");
    expect(routeSurfaceName(null)).toBe("home page");
  });
});
