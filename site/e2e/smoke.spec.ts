import { test, expect } from "@playwright/test";

test.describe("smoke", () => {
  test("home page loads", async ({ page }) => {
    await page.goto("/");
    await expect(page).toHaveTitle(/GauntletCI/);
    await expect(page.getByRole("link", { name: /GauntletCI/ }).first()).toBeVisible();
  });

  test("/about loads with heading and author bio", async ({ page }) => {
    await page.goto("/about");
    await expect(page.getByRole("heading", { name: "About", level: 1 })).toBeVisible();
    await expect(page.getByText("About the author").first()).toBeVisible();
  });

  test("/articles loads", async ({ page }) => {
    await page.goto("/articles");
    await expect(page).toHaveTitle(/GauntletCI/);
    await expect(page.getByRole("heading", { name: "Articles", level: 1 })).toBeVisible();
  });

  test("/docs loads", async ({ page }) => {
    await page.goto("/docs");
    await expect(page).toHaveTitle(/GauntletCI/);
    await expect(page.locator("h1").first()).toBeVisible();
  });

  test("/docs/rules loads with rule cards", async ({ page }) => {
    await page.goto("/docs/rules");
    await expect(page).toHaveTitle(/GauntletCI/);
    await expect(page.getByText("GCI0001").first()).toBeVisible();
  });

  test("/pricing loads", async ({ page }) => {
    await page.goto("/pricing");
    await expect(page).toHaveTitle(/GauntletCI/);
    await expect(page.locator("h1").first()).toBeVisible();
  });

  test("header nav includes About link", async ({ page }) => {
    await page.goto("/");
    await expect(page.locator("header").getByRole("link", { name: "About" })).toBeVisible();
  });

  test("footer has About link", async ({ page }) => {
    await page.goto("/");
    await expect(page.locator("footer").getByRole("link", { name: "About" })).toBeVisible();
  });

  test("footer Eric Cogen links to /about", async ({ page }) => {
    await page.goto("/");
    const ericLink = page.locator("footer").getByRole("link", { name: "Eric Cogen" });
    await expect(ericLink).toHaveAttribute("href", "/about");
  });
});
