import { MetadataRoute } from "next";
import { rules } from "@/lib/rules";

export const dynamic = "force-static";

const BASE_URL = "https://gauntletci.com";

export default function sitemap(): MetadataRoute.Sitemap {
  const ruleEntries: MetadataRoute.Sitemap = rules.map((r) => ({
    url: `${BASE_URL}/docs/rules/${r.id}`,
    changeFrequency: "monthly",
    priority: 0.7,
  }));

  // Pagination pages
  const paginationEntries: MetadataRoute.Sitemap = [
    { url: `${BASE_URL}/articles/p/2`, changeFrequency: "weekly", priority: 0.8 },
  ];

  return [
    { url: `${BASE_URL}/`,                                    changeFrequency: "weekly",  priority: 1.0 },
    { url: `${BASE_URL}/articles`,                            changeFrequency: "weekly",  priority: 0.9 },
    ...paginationEntries,
    { url: `${BASE_URL}/docs`,                                changeFrequency: "weekly",  priority: 0.9 },
    { url: `${BASE_URL}/docs/rules`,                          changeFrequency: "weekly",  priority: 0.9 },
    { url: `${BASE_URL}/docs/cli-reference`,                  changeFrequency: "monthly", priority: 0.8 },
    { url: `${BASE_URL}/docs/configuration`,                  changeFrequency: "monthly", priority: 0.8 },
    { url: `${BASE_URL}/docs/integrations`,                   changeFrequency: "monthly", priority: 0.8 },
    { url: `${BASE_URL}/docs/local-llm`,                      changeFrequency: "monthly", priority: 0.7 },
    { url: `${BASE_URL}/docs/custom-rules`,                    changeFrequency: "monthly", priority: 0.7 },
    { url: `${BASE_URL}/detections`,                          changeFrequency: "monthly", priority: 0.9 },
    { url: `${BASE_URL}/pricing`,                             changeFrequency: "monthly", priority: 0.8 },
    { url: `${BASE_URL}/releases`,                            changeFrequency: "monthly", priority: 0.8 },
    { url: `${BASE_URL}/about`,                               changeFrequency: "monthly", priority: 0.7 },
    { url: `${BASE_URL}/articles/why-tests-miss-bugs`,                 changeFrequency: "monthly", priority: 0.8 },
    { url: `${BASE_URL}/articles/why-code-review-misses-bugs`,         changeFrequency: "monthly", priority: 0.8 },
    { url: `${BASE_URL}/articles/detect-breaking-changes-before-merge`, changeFrequency: "monthly", priority: 0.8 },
    { url: `${BASE_URL}/articles/what-is-diff-based-analysis`,         changeFrequency: "monthly", priority: 0.7 },
    { url: `${BASE_URL}/articles/behavioral-change-risk-formal-framework`, changeFrequency: "monthly", priority: 0.8 },
    { url: `${BASE_URL}/articles/the-asymmetry-of-change`,             changeFrequency: "monthly", priority: 0.8 },
    { url: `${BASE_URL}/articles/can-ai-code-review-be-deterministic`, changeFrequency: "monthly", priority: 0.8 },
    { url: `${BASE_URL}/articles/jellyfin-pr-16062-post-mortem`,       changeFrequency: "monthly", priority: 0.8 },
    { url: `${BASE_URL}/articles/corpus-report-2025`,                  changeFrequency: "monthly", priority: 0.9 },
    { url: `${BASE_URL}/articles/azure-sdk-pr-57223-risk-analysis`,    changeFrequency: "monthly", priority: 0.8 },
    { url: `${BASE_URL}/articles/sonarqube-alternative-behavioral-gating`, changeFrequency: "monthly", priority: 0.7 },
    { url: `${BASE_URL}/articles/log4net-pr-201-analysis`,             changeFrequency: "monthly", priority: 0.8 },
    { url: `${BASE_URL}/articles/google-api-pr-3150-analysis`,         changeFrequency: "monthly", priority: 0.8 },
    { url: `${BASE_URL}/articles/stackexchange-redis-pr-3028`,         changeFrequency: "monthly", priority: 0.8 },
    { url: `${BASE_URL}/articles/grpc-dotnet-pr-2531`,                 changeFrequency: "monthly", priority: 0.8 },
    { url: `${BASE_URL}/articles/anglesharp-pr-1159-analysis`,         changeFrequency: "monthly", priority: 0.8 },
    { url: `${BASE_URL}/compare/gauntletci-vs-sonarqube`,     changeFrequency: "monthly", priority: 0.8 },
    { url: `${BASE_URL}/compare/gauntletci-vs-codeql`,        changeFrequency: "monthly", priority: 0.8 },
    { url: `${BASE_URL}/compare/gauntletci-vs-semgrep`,       changeFrequency: "monthly", priority: 0.8 },
    { url: `${BASE_URL}/compare/gauntletci-vs-snyk`,          changeFrequency: "monthly", priority: 0.8 },
    { url: `${BASE_URL}/compare/gauntletci-vs-codeclimate`,   changeFrequency: "monthly", priority: 0.8 },
    { url: `${BASE_URL}/compare/gauntletci-vs-ndepend`,       changeFrequency: "monthly", priority: 0.8 },
    { url: `${BASE_URL}/compare/gauntletci-vs-ai-code-review`, changeFrequency: "monthly", priority: 0.8 },
    { url: `${BASE_URL}/articles/case-studies`,                                                     changeFrequency: "monthly", priority: 0.9 },
    { url: `${BASE_URL}/benchmark`,                                                         changeFrequency: "monthly", priority: 0.9 },
    { url: `${BASE_URL}/articles/case-studies/stackexchange-redis-swallowed-exception`,             changeFrequency: "monthly", priority: 0.8 },
    { url: `${BASE_URL}/articles/case-studies/newtonsoft-json-assignment-in-getter`,                changeFrequency: "monthly", priority: 0.8 },
    { url: `${BASE_URL}/articles/case-studies/efcore-breaking-api-removal`,                        changeFrequency: "monthly", priority: 0.8 },
    { url: `${BASE_URL}/articles/case-studies/nunit-thread-sleep-async`,                           changeFrequency: "monthly", priority: 0.8 },
    { url: `${BASE_URL}/articles/case-studies/azuread-hardcoded-authority`,                        changeFrequency: "monthly", priority: 0.8 },
    ...ruleEntries,
  ];
}
