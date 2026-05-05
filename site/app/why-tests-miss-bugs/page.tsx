import type { Metadata } from "next";
import { redirect } from "next/navigation";

export const metadata: Metadata = {
  robots: "permanent-redirect",
  alternates: { canonical: "/articles/why-tests-miss-bugs" },
};

export default function WhyTestsMissBugsRedirect() {
  redirect("/articles/why-tests-miss-bugs");
}
