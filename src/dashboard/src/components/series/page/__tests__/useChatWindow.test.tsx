import { act, fireEvent, render, screen } from "@testing-library/react";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { CHAT_PAGE, useChatWindow } from "../useChatWindow";

// 2026-11-26-5201a: the series chat's window. jsdom lays nothing out, so every entry is 100px tall by the stub below and
// the box 500px: scrollHeight is the rendered entries' height.

const ENTRY_PX = 100;
const BOX_PX = 500;

function Chat({ count }: { count: number }) {
  const chat = useChatWindow(count);
  return (
    <div ref={chat.box} onScroll={chat.onScroll} data-testid="box">
      <div ref={chat.content}>
        {chat.from > 0 && <button type="button" onClick={chat.more} data-testid="earlier">earlier</button>}
        {Array.from({ length: count - chat.from }, (_, i) => <div key={chat.from + i} data-testid="entry">{chat.from + i}</div>)}
      </div>
    </div>
  );
}

const box = () => screen.getByTestId("box");
const shown = () => screen.getAllByTestId("entry").map((e) => Number(e.textContent));
const scrollTo = (top: number) => { box().scrollTop = top; fireEvent.scroll(box()); };

let restore: (() => void) | null = null;
beforeEach(() => {
  const height = Object.getOwnPropertyDescriptor(HTMLElement.prototype, "scrollHeight");
  const client = Object.getOwnPropertyDescriptor(HTMLElement.prototype, "clientHeight");
  Object.defineProperty(HTMLElement.prototype, "scrollHeight", {
    configurable: true, get(this: HTMLElement) { return this.querySelectorAll("[data-testid=entry]").length * ENTRY_PX; },
  });
  Object.defineProperty(HTMLElement.prototype, "clientHeight", { configurable: true, get: () => BOX_PX });
  restore = () => {
    if (height) Object.defineProperty(HTMLElement.prototype, "scrollHeight", height);
    if (client) Object.defineProperty(HTMLElement.prototype, "clientHeight", client);
  };
});
afterEach(() => restore?.());

describe("useChatWindow", () => {
  it("ChatWindow_ALongTranscript_RendersTheNewestPage_OpenedAtTheBottom", () => {
    render(<Chat count={100} />);

    expect(shown()).toHaveLength(CHAT_PAGE);
    expect(shown()[0]).toBe(100 - CHAT_PAGE);
    expect(box().scrollTop).toBe(CHAT_PAGE * ENTRY_PX);
  });

  it("ChatWindow_ScrolledToTheTop_RendersOlderEntries_KeepingThePlace", () => {
    render(<Chat count={100} />);

    act(() => scrollTo(40));

    expect(shown()[0]).toBe(100 - 2 * CHAT_PAGE);
    expect(box().scrollTop).toBe(40 + CHAT_PAGE * ENTRY_PX);
  });

  it("ChatWindow_TheEarlierButton_RendersOlderEntries_UntilTheFirst", () => {
    render(<Chat count={40} />);

    fireEvent.click(screen.getByTestId("earlier"));

    expect(shown()).toHaveLength(40);
    expect(screen.queryByTestId("earlier")).toBeNull();
  });

  it("ChatWindow_ANewEntryAtTheBottom_StaysPinned", () => {
    const page = render(<Chat count={40} />);

    page.rerender(<Chat count={41} />);

    expect(shown()).toHaveLength(CHAT_PAGE + 1);
    expect(box().scrollTop).toBe((CHAT_PAGE + 1) * ENTRY_PX);
  });

  it("ChatWindow_ANewEntryWhileScrolledUp_MovesNothing", () => {
    const page = render(<Chat count={40} />);
    act(() => scrollTo(1500));

    page.rerender(<Chat count={41} />);

    expect(shown()[0]).toBe(40 - CHAT_PAGE);
    expect(box().scrollTop).toBe(1500);
  });

  it("ChatWindow_ATranscriptReplacedByAShorterOne_StartsOverAtItsNewest", () => {
    const page = render(<Chat count={100} />);
    act(() => scrollTo(40));

    page.rerender(<Chat count={20} />);

    expect(shown()).toHaveLength(20);
    expect(box().scrollTop).toBe(20 * ENTRY_PX);
  });

  it("ChatWindow_ContentGrowingAtTheBottom_KeepsTheReaderThere", () => {
    let grow: () => void = () => {};
    vi.stubGlobal("ResizeObserver", class {
      constructor(callback: () => void) { grow = callback; }
      observe() {}
      disconnect() {}
    });
    render(<Chat count={10} />);
    box().scrollTop = 0; // as if a turn's steps had just made the content taller than where the box stood

    act(() => grow());

    expect(box().scrollTop).toBe(10 * ENTRY_PX);
    vi.unstubAllGlobals();
  });

  it("ChatWindow_AShortTranscript_RendersAllOfIt", () => {
    render(<Chat count={3} />);

    expect(shown()).toEqual([0, 1, 2]);
    expect(screen.queryByTestId("earlier")).toBeNull();
  });
});
