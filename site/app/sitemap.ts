import { MetadataRoute } from "next";

export const dynamic = "force-static";

const BASE_URL = "https://gauntletci.com";

export default function sitemap(): MetadataRoute.Sitemap {
  return [
    { url: `${BASE_URL}/`,                                    changeFrequency: "weekly",  priority: 1.0 },
    { url: `${BASE_URL}/articles`,                            changeFrequency: "weekly",  priority: 0.9 },
    { url: `${BASE_URL}/docs`,                                changeFrequency: "weekly",  priority: 0.9 },
    { url: `${BASE_URL}/docs/rules`,                          changeFrequency: "weekly",  priority: 0.9 },
    { url: `${BASE_URL}/docs/cli-reference`,                  changeFrequency: "monthly", priority: 0.8 },
    { url: `${BASE_URL}/docs/configuration`,                  changeFrequency: "monthly", priority: 0.8 },
    { url: `${BASE_URL}/docs/integrations`,                   changeFrequency: "monthly", priority: 0.8 },
    { url: `${BASE_URL}/docs/local-llm`,                      changeFrequency: "monthly", priority: 0.7 },
    { url: `${BASE_URL}/detections`,                          changeFrequency: "monthly", priority: 0.9 },
    { url: `${BASE_URL}/pricing`,                             changeFrequency: "monthly", priority: 0.8 },
    { url: `${BASE_URL}/why-tests-miss-bugs`,                 changeFrequency: "monthly", priority: 0.8 },
    { url: `${BASE_URL}/why-code-review-misses-bugs`,         changeFrequency: "monthly", priority: 0.8 },
    { url: `${BASE_URL}/detect-breaking-changes-before-merge`, changeFrequency: "monthly", priority: 0.8 },
    { url: `${BASE_URL}/what-is-diff-based-analysis`,         changeFrequency: "monthly", priority: 0.7 },
    { url: `${BASE_URL}/behavioral-change-risk-formal-framework`, changeFrequency: "monthly", priority: 0.8 },
    { url: `${BASE_URL}/compare/gauntletci-vs-sonarqube`,     changeFrequency: "monthly", priority: 0.8 },
    { url: `${BASE_URL}/compare/gauntletci-vs-codeql`,        changeFrequency: "monthly", priority: 0.8 },
    { url: `${BASE_URL}/compare/gauntletci-vs-semgrep`,       changeFrequency: "monthly", priority: 0.8 },
    { url: `${BASE_URL}/compare/gauntletci-vs-snyk`,          changeFrequency: "monthly", priority: 0.8 },
    { url: `${BASE_URL}/compare/gauntletci-vs-codeclimate`,   changeFrequency: "monthly", priority: 0.8 },
  ];
}
