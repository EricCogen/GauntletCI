import type { Metadata } from "next";
import { redirect } from "next/navigation";

export const metadata: Metadata = {
  robots: "permanent-redirect",
  alternates: { canonical: "/articles/can-ai-code-review-be-deterministic" },
};

export default function CanAICodeReviewBeDeterministicRedirect() {
  redirect("/articles/can-ai-code-review-be-deterministic");
}
