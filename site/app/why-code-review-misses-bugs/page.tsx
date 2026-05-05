import type { Metadata } from "next";
import { redirect } from "next/navigation";

export const metadata: Metadata = {
  robots: "permanent-redirect",
  alternates: { canonical: "/articles/why-code-review-misses-bugs" },
};

export default function WhyCodeReviewMissesBugsRedirect() {
  redirect("/articles/why-code-review-misses-bugs");
}
