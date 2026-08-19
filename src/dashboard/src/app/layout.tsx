import type { Metadata } from "next";
import { Inter } from "next/font/google";
import { AppRail } from "@/components/shell/AppRail";
import { DegradedBanner } from "@/components/shell/DegradedBanner";
import { ConfigCatalogProvider } from "@/components/config/ConfigCatalogProvider";
import { EventStoreProvider } from "@/lib/eventStore/EventStoreProvider";
import { RunBucketFilterProvider } from "@/lib/RunBucketFilter";
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
        {/* p0353: the config catalog is a SINGLE shared instance above both the
            rail and the config page, so an import/save/revert's reload() refreshes
            the rail's count badges and the studio pane in one shot. */}
        {/* p0458: the monitor bucket the rail selects is shared by the rail and
            the run list below it, so the highlighted item and the visible
            sections are always the same answer. */}
        <EventStoreProvider>
          <ConfigCatalogProvider>
            <RunBucketFilterProvider>
              <div className="grid min-h-screen grid-cols-[230px_1fr]">
                <AppRail />
                {/* p0391a: the server always starts, so "it came up" no longer means
                    "it is fine" — the banner names what is down, above every route. */}
                <main className="h-screen overflow-y-auto">
                  <DegradedBanner />
                  {children}
                </main>
              </div>
            </RunBucketFilterProvider>
          </ConfigCatalogProvider>
        </EventStoreProvider>
      </body>
    </html>
  );
}
