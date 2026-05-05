import type { Metadata } from "next";
import { redirect } from "next/navigation";

export const metadata: Metadata = {
  robots: "permanent-redirect",
  alternates: { canonical: "/articles/case-studies/stackexchange-redis-swallowed-exception" },
};

export default function CaseStudyRedirect() {
  redirect("/articles/case-studies/stackexchange-redis-swallowed-exception");
}
