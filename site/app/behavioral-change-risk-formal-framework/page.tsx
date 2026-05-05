import type { Metadata } from "next";
import { redirect } from "next/navigation";

export const metadata: Metadata = {
  robots: "permanent-redirect",
  alternates: { canonical: "/articles/behavioral-change-risk-formal-framework" },
};

export default function BehavioralChangeRiskRedirect() {
  redirect("/articles/behavioral-change-risk-formal-framework");
}
