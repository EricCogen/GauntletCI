import type { Metadata } from "next";
import { redirect } from "next/navigation";

export const metadata: Metadata = {
  robots: "permanent-redirect",
  alternates: { canonical: "/articles/detect-breaking-changes-before-merge" },
};

export default function DetectBreakingChangesRedirect() {
  redirect("/articles/detect-breaking-changes-before-merge");
}
