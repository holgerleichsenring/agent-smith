import { describe, it, expect, vi } from "vitest";
import { render, screen, fireEvent } from "@testing-library/react";
import { ProjectMachineSection } from "../ProjectMachineSection";
import type { StudioEntity, StudioProject } from "@/lib/configApi";

// 2026-11-25-9992h: the machine identity agent-smith acts under, edited on the project form.

const project: StudioProject = {
  id: "p", agent: "a", tracker: "t", repos: ["r"], pipeline: "", pipelines: [], resolution: null,
};

const secrets = [{ id: "GH_APP_KEY" }, { id: "GITLAB_BOT" }] as StudioEntity[];

describe("ProjectMachineSection", () => {
  it("writes the name, the email and a GitHub App entry", () => {
    const onChange = vi.fn();
    const { rerender } = render(<ProjectMachineSection project={project} onChange={onChange} secrets={secrets} />);
    fireEvent.change(screen.getByTestId("form-field-machine-name"), { target: { value: "agent-smith-bot" } });
    expect(onChange.mock.calls.at(-1)![0].machine).toEqual({ name: "agent-smith-bot", email: "" });

    const named = { ...project, machine: { name: "agent-smith-bot", email: "bot@example.com" } };
    rerender(<ProjectMachineSection project={named} onChange={onChange} secrets={secrets} />);
    fireEvent.change(screen.getByTestId("form-field-machine-github-auth"), { target: { value: "GH_APP_KEY" } });
    expect(onChange.mock.calls.at(-1)![0].machine.github).toEqual({ auth: "GH_APP_KEY" });

    const withKey = { ...project, machine: { ...named.machine, github: { auth: "GH_APP_KEY" } } };
    rerender(<ProjectMachineSection project={withKey} onChange={onChange} secrets={secrets} />);
    fireEvent.change(screen.getByTestId("form-field-machine-github-appId"), { target: { value: "123" } });
    expect(onChange.mock.calls.at(-1)![0].machine.github).toEqual({ auth: "GH_APP_KEY", appId: "123" });
  });

  it("offers the app fields on GitHub only and clears a hoster whose fields are all empty", () => {
    const onChange = vi.fn();
    const gitlab = { ...project, machine: { name: "bot", email: "bot@example.com", gitlab: { auth: "GITLAB_BOT" } } };
    render(<ProjectMachineSection project={gitlab} onChange={onChange} secrets={secrets} />);
    expect(screen.queryByTestId("form-field-machine-gitlab-appId")).toBeNull();
    expect(screen.getByTestId("form-field-machine-github-appId")).toBeTruthy();
    fireEvent.change(screen.getByTestId("form-field-machine-gitlab-auth"), { target: { value: "" } });
    expect(onChange.mock.calls.at(-1)![0].machine.gitlab).toBeNull();
  });
});
