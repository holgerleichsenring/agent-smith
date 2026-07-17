import type { Metadata } from "next";
import { Inter } from "next/font/google";
import { AppRail } from "@/components/shell/AppRail";
import { EventStoreProvider } from "@/lib/eventStore/EventStoreProvider";
import "./globals.css";

// p0174: Inter is the DESIGN.md primary typography — load via next/font
// so it's self-hosted, font-display:swap by default, and bound to a CSS
// variable the rest of the app consumes via Tailwind's font-sans utility.
const inter = Inter({
  subsets: ["latin"],
  variable: "--font-sans",
  display: "swap",
});

export const metadata: Metadata = {
  title: "agent-smith",
  description: "AI orchestration framework — self-hosted",
};

export default function RootLayout({ children }: { children: React.ReactNode }) {
  return (
    <html lang="en" className={inter.variable}>
      <body className="bg-[var(--color-canvas)] text-[var(--color-ink)] font-sans">
        {/* p0209a: persistent left app rail + scrollable full-height main.
            [248px 1fr] grid replaces the topbar; every route renders inside. */}
        {/* p0218: the shared EventStore lives above every route so the system
            backlog survives navigation and one subscription feeds all views. */}
        {/* p0343c: the mock shell — 230px rail per the ratified mockups' .app grid. */}
        <EventStoreProvider>
          <div className="grid min-h-screen grid-cols-[230px_1fr]">
            <AppRail />
            <main className="h-screen overflow-y-auto">{children}</main>
          </div>
        </EventStoreProvider>
      </body>
    </html>
  );
}
