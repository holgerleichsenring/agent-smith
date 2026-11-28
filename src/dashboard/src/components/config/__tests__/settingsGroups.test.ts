import { describe, it, expect } from "vitest";
import {
  SETTING_GROUPS,
  SETTING_KEYS,
  SETTINGS_NAV,
  changedCount,
  groupOf,
  isSettingKey,
  settingHref,
  settingsNavHref,
} from "../settings";

// 2026-11-28-9bd1: the settings rail as groups of sections beside the pages of their own, and the
// count a section's heading carries.

describe("settings groups", () => {
  it("EveryKey_IsInExactlyOneNavEntry", () => {
    const placed = SETTINGS_NAV.flatMap((entry) => (entry.kind === "group" ? entry.group.members : [entry.key]));
    expect([...placed].sort()).toEqual([...SETTING_KEYS].sort());
    expect(new Set(placed).size).toBe(placed.length);
  });

  it("NoGroupSlug_IsASettingKey", () => {
    for (const group of SETTING_GROUPS) expect(isSettingKey(group.id)).toBe(false);
  });

  it("TheRail_IsTheApprovedGroupsAndPages", () => {
    expect(SETTINGS_NAV.map(settingsNavHref)).toEqual([
      "/config/settings/runs",
      "/config/settings/workflow",
      "/config/settings/sandbox",
      "/config/settings/series",
      "/config/settings/tickets",
      "/config/settings/pocs",
      "/config/settings/skills",
      "/config/settings/diagnostics",
    ]);
    expect(groupOf("queue")?.members).toEqual([
      "orchestrator", "primary_provider", "limits", "pipeline_cost_cap", "queue", "deployment",
    ]);
    expect(groupOf("holds")?.members).toEqual(["dialogue", "spec_review", "preconditions", "holds", "branch_cleanup"]);
    expect(groupOf("trace")?.members).toEqual(["pipeline_data_flow", "trace"]);
  });

  it("SettingHref_ASection_IsItsGroupWithTheSectionOpen_APage_IsItself", () => {
    expect(settingHref("queue")).toBe("/config/settings/runs?open=queue");
    expect(settingHref("holds")).toBe("/config/settings/workflow?open=holds");
    expect(settingHref("sandbox")).toBe("/config/settings/sandbox");
  });

  it("ChangedCount_CountsDifferingLeaves_ArrayOnce", () => {
    expect(changedCount({ a: 1, b: { c: 2, d: 3 } }, { a: 1, b: { c: 2, d: 3 } })).toBe(0);
    expect(changedCount({ a: 2, b: { c: 2, d: 4 } }, { a: 1, b: { c: 2, d: 3 } })).toBe(2);
    expect(changedCount({ list: ["x", "y"] }, { list: [] })).toBe(1);
    expect(changedCount({ list: ["x"] }, { list: ["x"] })).toBe(0);
  });

  it("ChangedCount_NullAndAbsent_AreTheSame_AMissingSubObjectCountsItsLeaves", () => {
    expect(changedCount({ a: null }, {})).toBe(0);
    expect(changedCount({ value: "claude" }, { value: null })).toBe(1);
    // A cost-cap tier the doc leaves out differs in its usd and its tokens.
    expect(changedCount({ perTier: {} }, { perTier: { Xl: { usd: 50, tokens: 10 } } })).toBe(2);
  });
});
