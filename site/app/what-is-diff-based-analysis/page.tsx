import type { Metadata } from "next";
import { redirect } from "next/navigation";

export const metadata: Metadata = {
  robots: "permanent-redirect",
  alternates: { canonical: "/articles/what-is-diff-based-analysis" },
};

export default function WhatIsDiffBasedAnalysisRedirect() {
  redirect("/articles/what-is-diff-based-analysis");
}
