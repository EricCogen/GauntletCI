import { test, expect } from "@playwright/test";

const ARTICLES = [
  { slug: "why-tests-miss-bugs",                       heading: /why tests miss bugs/i },
  { slug: "why-code-review-misses-bugs",               heading: /code review blind spots/i },
  { slug: "detect-breaking-changes-before-merge",      heading: /detect breaking changes/i },
  { slug: "behavioral-change-risk-formal-framework",   heading: /behavioral change risk/i },
  { slug: "what-is-diff-based-analysis",               heading: /diff.based analysis/i },
];

test.describe("article pages", () => {
  for (const { slug, heading } of ARTICLES) {
    test(`${slug} has h1, rules-applied, and author bio`, async ({ page }) => {
      await page.goto(`/${slug}`);
      await expect(page.getByRole("heading", { level: 1 }).filter({ hasText: heading })).toBeVisible();
      await expect(page.getByText("Rules applied in this article")).toBeVisible();
      await expect(page.getByText("About the author")).toBeVisible();
    });
  }
});
