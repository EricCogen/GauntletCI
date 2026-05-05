import type { Metadata } from "next";
import { redirect } from "next/navigation";

export const metadata: Metadata = {
  robots: "permanent-redirect",
  alternates: { canonical: "/articles/case-studies/efcore-breaking-api-removal" },
};

export default function CaseStudyRedirect() {
  redirect("/articles/case-studies/efcore-breaking-api-removal");
}
