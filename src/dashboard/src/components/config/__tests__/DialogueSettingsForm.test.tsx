import { describe, it, expect, vi } from "vitest";
import { render, screen, fireEvent } from "@testing-library/react";
import { SettingsForm } from "../SettingsForm";
import type { DialogueSetting } from "@/lib/configApi";

// 2026-11-28-d1ffb: whether a series question's recommendation is taken by default at all sits beside its minutes, and both
// help texts name the value, the reason and the key.

const dialogue: DialogueSetting = {
  hotWaitSeconds: 600, approvalTimeoutSeconds: 259200, mockMaxChars: 90000, mockVersionsPerSpec: 20, mockVersionsPerConversation: 100,
  seriesMasterBudgetUsd: 20, seriesStatusMinutes: 15, questionDefaultMinutes: 120,
};

describe("Dialogue settings", () => {
  it("reads an absent toggle as on and turns the default off", () => {
    const onChange = vi.fn();
    render(<SettingsForm settingKey="dialogue" value={dialogue} onChange={onChange} capabilities={null} />);

    const toggle = screen.getByTestId("setting-dialogue-questiondefault-enabled");
    expect(toggle.getAttribute("data-selected")).toBe("true");
    fireEvent.click(toggle);
    expect(onChange.mock.calls.at(-1)![0]).toMatchObject({ questionDefaultEnabled: false, questionDefaultMinutes: 120 });
  });

  it("shows a stored off as off", () => {
    render(<SettingsForm settingKey="dialogue" value={{ ...dialogue, questionDefaultEnabled: false }} onChange={vi.fn()} capabilities={null} />);

    expect(screen.getByTestId("setting-dialogue-questiondefault-enabled").getAttribute("data-selected")).toBe("false");
  });

  it("names value, reason and key in both help texts", () => {
    render(<SettingsForm settingKey="dialogue" value={dialogue} onChange={vi.fn()} capabilities={null} />);

    expect(screen.getByText(/On by default\..*Key: dialogue\.question_default_enabled/)).toBeTruthy();
    expect(screen.getByText(/120 by default\..*does not hold the series up.*Key: dialogue\.question_default_minutes/)).toBeTruthy();
  });
});
