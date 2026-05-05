import type { Metadata } from "next";
import { redirect } from "next/navigation";

export const metadata: Metadata = {
  robots: "permanent-redirect",
  alternates: { canonical: "/articles/case-studies" },
};

export default function CaseStudiesRedirect() {
  redirect("/articles/case-studies");
}
