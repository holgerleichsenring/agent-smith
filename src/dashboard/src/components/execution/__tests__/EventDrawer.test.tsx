import { render, screen, fireEvent } from "@testing-library/react";
import { describe, it, expect } from "vitest";
import { EventDrawer, type DrawerEvent } from "../EventDrawer";

function ev(
  id: string,
  ts: string,
  kind: DrawerEvent["kind"],
  text: string,
): DrawerEvent {
  return { id, timestamp: ts, kind, body: <span>{text}</span>, searchText: text };
}

describe("EventDrawer", () => {
  const events: DrawerEvent[] = [
    ev("1", "16:00:01", "obs", "observation alpha"),
    ev("2", "16:00:02", "find", "finding beta"),
    ev("3", "16:00:03", "tool", "tool call gamma"),
    ev("4", "16:00:04", "llm", "llm call delta"),
    ev("5", "16:00:05", "file", "file written epsilon"),
    ev("6", "16:00:06", "dec", "decision zeta"),
  ];

  it("EventDrawer_FilterChipAll_ShowsEveryKind", () => {
    render(<EventDrawer events={events} />);
    expect(screen.getByTestId("event-drawer-list").children).toHaveLength(events.length);
  });

  it("EventDrawer_KindChipToggle_NarrowsToSelectedKinds", () => {
    render(<EventDrawer events={events} />);
    fireEvent.click(screen.getByTestId("event-drawer-chip-find"));
    expect(screen.getByTestId("event-drawer-list").children).toHaveLength(1);
    expect(screen.getByText("finding beta")).toBeInTheDocument();
  });

  it("EventDrawer_SortToggle_SwapsNewestFirstOldestFirst", () => {
    render(<EventDrawer events={events} />);
    const list = screen.getByTestId("event-drawer-list");
    expect(list.firstChild?.textContent).toContain("16:00:06");
    fireEvent.click(screen.getByTestId("event-drawer-sort"));
    expect(list.firstChild?.textContent).toContain("16:00:01");
  });

  it("EventDrawer_ContentSearch_FiltersInPlace", () => {
    render(<EventDrawer events={events} />);
    fireEvent.change(screen.getByTestId("event-drawer-search"), {
      target: { value: "gamma" },
    });
    const list = screen.getByTestId("event-drawer-list");
    expect(list.children).toHaveLength(1);
    expect(list.textContent).toContain("tool call gamma");
  });

  it("EventDrawer_DefaultCapAtN_ShowsExpanderWhenOver", () => {
    const many: DrawerEvent[] = Array.from({ length: 12 }, (_, i) =>
      ev(`m${i}`, `16:00:${String(i).padStart(2, "0")}`, "obs", `o${i}`),
    );
    render(<EventDrawer events={many} defaultCap={5} />);
    expect(screen.getByTestId("event-drawer-list").children).toHaveLength(5);
    const expander = screen.getByTestId("event-drawer-show-all");
    expect(expander).toHaveTextContent("show all 12 events");
  });

  it("EventDrawer_ShowAllExpander_RevealsFullList", () => {
    const many: DrawerEvent[] = Array.from({ length: 12 }, (_, i) =>
      ev(`m${i}`, `16:00:${String(i).padStart(2, "0")}`, "obs", `o${i}`),
    );
    render(<EventDrawer events={many} defaultCap={5} />);
    fireEvent.click(screen.getByTestId("event-drawer-show-all"));
    expect(screen.getByTestId("event-drawer-list").children).toHaveLength(12);
  });

  it("EventDrawer_DeselectingAllChips_FallsBackToAll", () => {
    render(<EventDrawer events={events} />);
    fireEvent.click(screen.getByTestId("event-drawer-chip-find"));
    fireEvent.click(screen.getByTestId("event-drawer-chip-find"));
    expect(screen.getByTestId("event-drawer-chip-all").dataset.active).toBe("true");
  });
});
