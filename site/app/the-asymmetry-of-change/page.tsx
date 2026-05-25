import type { Metadata } from "next";
import { redirect } from "next/navigation";

export const metadata: Metadata = {
  robots: "permanent-redirect",
  alternates: { canonical: "/articles/the-asymmetry-of-change" },
};

export default function TheAsymmetryOfChangeRedirect() {
  redirect("/articles/the-asymmetry-of-change");
}
