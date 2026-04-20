"use client";
import { useEffect } from "react";
export default function RulesRedirect() {
  useEffect(() => { window.location.replace("/docs/rules"); }, []);
  return null;
}
