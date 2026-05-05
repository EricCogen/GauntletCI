import type { Metadata } from "next";
import { redirect } from "next/navigation";

export const metadata: Metadata = {
  robots: "permanent-redirect",
  alternates: { canonical: "/articles/case-studies/nunit-thread-sleep-async" },
};

export default function CaseStudyRedirect() {
  redirect("/articles/case-studies/nunit-thread-sleep-async");
}
