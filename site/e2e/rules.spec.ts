import { test, expect } from "@playwright/test";

const RULE_IDS = [
  "GCI0001", "GCI0003", "GCI0004", "GCI0006", "GCI0007",
  "GCI0010", "GCI0012", "GCI0015", "GCI0016", "GCI0021",
  "GCI0022", "GCI0024", "GCI0029", "GCI0032", "GCI0035",
  "GCI0036", "GCI0038", "GCI0039", "GCI0041", "GCI0042",
  "GCI0043", "GCI0044", "GCI0045", "GCI0046", "GCI0047",
  "GCI0048", "GCI0049", "GCI0050", "GCI0052", "GCI0053",
];

test.describe("rule detail pages", () => {
  for (const ruleId of RULE_IDS) {
    test(`${ruleId} page renders`, async ({ page }) => {
      await page.goto(`/docs/rules/${ruleId}`);
      await expect(page.getByText(ruleId).first()).toBeVisible();
      await expect(page.getByText("Why this rule exists")).toBeVisible();
      await expect(page.getByText("About the author")).toBeVisible();
    });
  }

  test("rule index links to first rule page", async ({ page }) => {
    await page.goto("/docs/rules");
    await page.getByRole("link", { name: /GCI0001/ }).first().click();
    await expect(page).toHaveURL(/GCI0001/);
    await expect(page.getByText("GCI0001").first()).toBeVisible();
  });
});
