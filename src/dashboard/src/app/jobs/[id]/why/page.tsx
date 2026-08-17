"use client";

import { use } from "react";
import { RunWhy } from "@/components/jobs/why/RunWhy";

// p0423b: the story view has its OWN route. Progress-watching and failure-diagnosis are
// different jobs and must not share a screen — the live view stays at /jobs/{id} and
// carries no statistics; this one is opened deliberately when the question changes from
// "what is happening" to "why did this run do that".

interface PageProps {
  params: Promise<{ id: string }>;
}

export default function RunWhyPage({ params }: PageProps) {
  const { id } = use(params);
  return <RunWhy runId={decodeURIComponent(id)} />;
}
