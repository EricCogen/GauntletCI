import { createHmac, createPrivateKey, createSign, timingSafeEqual } from "node:crypto";
import { createServer, IncomingMessage, ServerResponse } from "node:http";
import { mkdir, rm, writeFile } from "node:fs/promises";
import { join, resolve } from "node:path";
import { spawn } from "node:child_process";

interface Env {
  appId: string;
  privateKey: string;
  webhookSecret: string;
  port: number;
  gauntletciCommand: string;
  sensitivity: string;
  severity: string;
  autoReviewPullRequests: boolean;
  workDir: string;
}

interface GitHubRepo {
  name: string;
  full_name: string;
  clone_url: string;
  owner: { login: string };
}

interface GitHubInstallation {
  id: number;
}

interface PullRequestPayload {
  action: string;
  installation?: GitHubInstallation;
  repository: GitHubRepo;
  pull_request: {
    number: number;
    head: { sha: string };
    html_url: string;
  };
}

interface IssueCommentPayload {
  action: string;
  installation?: GitHubInstallation;
  repository: GitHubRepo;
  issue: {
    number: number;
    pull_request?: { url: string };
  };
  comment: {
    body: string;
    user?: { login: string; type?: string };
  };
}

interface Finding {
  RuleId?: string;
  ruleId?: string;
  Rule?: string;
  Title?: string;
  title?: string;
  Message?: string;
  message?: string;
  Severity?: number | string;
  severity?: number | string;
  FilePath?: string;
  filePath?: string;
  Path?: string;
  path?: string;
  Line?: number;
  line?: number;
}

interface GauntletResult {
  Findings?: Finding[];
  findings?: Finding[];
}

const env = loadEnv();

const server = createServer(async (request, response) => {
  try {
    await route(request, response);
  } catch (error) {
    console.error("Unhandled request error", error);
    sendText(response, 500, "Internal server error");
  }
});

server.listen(env.port, "0.0.0.0", () => {
  console.log(`GauntletCI GitHub App server listening on :${env.port}`);
});

async function route(request: IncomingMessage, response: ServerResponse): Promise<void> {
  const url = new URL(request.url ?? "/", `http://${request.headers.host ?? "localhost"}`);

  if (request.method === "GET" && (url.pathname === "/health" || url.pathname === "/healthz")) {
    sendText(response, 200, "OK");
    return;
  }

  if (request.method === "POST" && url.pathname === "/github/webhook") {
    await handleGitHubWebhook(request, response);
    return;
  }

  sendText(response, 404, "Not found");
}

async function handleGitHubWebhook(request: IncomingMessage, response: ServerResponse): Promise<void> {
  const rawBody = await readRequestBody(request);
  const signature = request.headers["x-hub-signature-256"];

  if (typeof signature !== "string" || !verifyGitHubSignature(rawBody, signature, env.webhookSecret)) {
    sendText(response, 401, "Invalid signature");
    return;
  }

  const event = request.headers["x-github-event"];
  if (typeof event !== "string") {
    sendText(response, 400, "Missing X-GitHub-Event");
    return;
  }

  const payload = parseJson(rawBody);
  if (!payload) {
    sendText(response, 400, "Invalid JSON");
    return;
  }

  if (event === "ping") {
    sendText(response, 200, "pong");
    return;
  }

  if (event === "issue_comment") {
    await handleIssueComment(payload as IssueCommentPayload);
    sendText(response, 200, "OK");
    return;
  }

  if (event === "pull_request") {
    await handlePullRequest(payload as PullRequestPayload);
    sendText(response, 200, "OK");
    return;
  }

  sendText(response, 200, "Ignored");
}

async function handleIssueComment(payload: IssueCommentPayload): Promise<void> {
  if (payload.action !== "created") return;
  if (!payload.issue.pull_request) return;
  if (!payload.comment.body.toLowerCase().includes("@gauntletci review")) return;
  if (payload.comment.user?.type === "Bot") return;

  const token = await createInstallationToken(requiredInstallationId(payload.installation));
  const pullRequest = await getPullRequest(payload.repository, payload.issue.number, token);

  await reviewPullRequest({
    repository: payload.repository,
    pullNumber: payload.issue.number,
    headSha: pullRequest.head.sha,
    htmlUrl: pullRequest.html_url,
    installationToken: token,
    trigger: "@gauntletci review",
  });
}

async function handlePullRequest(payload: PullRequestPayload): Promise<void> {
  if (!env.autoReviewPullRequests) return;
  if (!["opened", "synchronize", "reopened"].includes(payload.action)) return;

  const token = await createInstallationToken(requiredInstallationId(payload.installation));
  await reviewPullRequest({
    repository: payload.repository,
    pullNumber: payload.pull_request.number,
    headSha: payload.pull_request.head.sha,
    htmlUrl: payload.pull_request.html_url,
    installationToken: token,
    trigger: `pull_request.${payload.action}`,
  });
}

async function reviewPullRequest(input: {
  repository: GitHubRepo;
  pullNumber: number;
  headSha: string;
  htmlUrl: string;
  installationToken: string;
  trigger: string;
}): Promise<void> {
  const runId = `${Date.now()}-${input.repository.full_name.replace(/[^\w.-]+/g, "-")}-${input.pullNumber}`;
  const workRoot = resolve(env.workDir, runId);
  const repoDir = join(workRoot, "repo");
  const diffPath = join(workRoot, "pr.diff");

  console.log(`Reviewing ${input.repository.full_name}#${input.pullNumber} at ${input.headSha}`);

  let checkRunId: number | undefined;
  try {
    checkRunId = await createCheckRun(input.repository, input.headSha, input.installationToken);
    await mkdir(workRoot, { recursive: true });

    const diff = await fetchPullRequestDiff(input.repository, input.pullNumber, input.installationToken);
    await writeFile(diffPath, diff, "utf8");
    await cloneRepository(input.repository, input.headSha, repoDir, input.installationToken);

    const analysis = await runGauntletCI(diffPath, repoDir);
    const parsed = parseGauntletResult(analysis.stdout);
    const findings = getFindings(parsed);
    const conclusion = findings.length > 0 ? "failure" : "success";
    const summary = buildSummary(input, findings, analysis.exitCode);

    if (checkRunId) {
      await completeCheckRun(input.repository, checkRunId, conclusion, summary, input.installationToken);
    }
    await postIssueComment(input.repository, input.pullNumber, summary, input.installationToken);
  } catch (error) {
    console.error(`GauntletCI review failed for ${input.repository.full_name}#${input.pullNumber}`, error);
    const body = buildFailureSummary(input, error);
    if (checkRunId) {
      await completeCheckRun(input.repository, checkRunId, "failure", body, input.installationToken);
    }
    await postIssueComment(input.repository, input.pullNumber, body, input.installationToken);
  } finally {
    await rm(workRoot, { recursive: true, force: true });
  }
}

async function createInstallationToken(installationId: number): Promise<string> {
  const jwt = createGitHubAppJwt();
  const response = await githubFetch(
    `https://api.github.com/app/installations/${installationId}/access_tokens`,
    {
      method: "POST",
      headers: {
        Authorization: `Bearer ${jwt}`,
      },
    }
  );

  const body = await response.json() as { token?: string };
  if (!body.token) throw new Error("GitHub did not return an installation token");
  return body.token;
}

async function getPullRequest(repository: GitHubRepo, pullNumber: number, token: string): Promise<{ head: { sha: string }, html_url: string }> {
  const response = await githubFetch(
    `https://api.github.com/repos/${repository.full_name}/pulls/${pullNumber}`,
    {
      headers: {
        Authorization: `Bearer ${token}`,
      },
    }
  );
  return response.json() as Promise<{ head: { sha: string }, html_url: string }>;
}

async function fetchPullRequestDiff(repository: GitHubRepo, pullNumber: number, token: string): Promise<string> {
  const response = await githubFetch(
    `https://api.github.com/repos/${repository.full_name}/pulls/${pullNumber}`,
    {
      headers: {
        Accept: "application/vnd.github.v3.diff",
        Authorization: `Bearer ${token}`,
      },
    }
  );
  return response.text();
}

async function createCheckRun(repository: GitHubRepo, headSha: string, token: string): Promise<number> {
  const response = await githubFetch(
    `https://api.github.com/repos/${repository.full_name}/check-runs`,
    {
      method: "POST",
      headers: {
        Authorization: `Bearer ${token}`,
        "Content-Type": "application/json",
      },
      body: JSON.stringify({
        name: "GauntletCI",
        head_sha: headSha,
        status: "in_progress",
        started_at: new Date().toISOString(),
      }),
    }
  );

  const body = await response.json() as { id: number };
  return body.id;
}

async function completeCheckRun(
  repository: GitHubRepo,
  checkRunId: number,
  conclusion: "success" | "failure",
  summary: string,
  token: string
): Promise<void> {
  await githubFetch(
    `https://api.github.com/repos/${repository.full_name}/check-runs/${checkRunId}`,
    {
      method: "PATCH",
      headers: {
        Authorization: `Bearer ${token}`,
        "Content-Type": "application/json",
      },
      body: JSON.stringify({
        status: "completed",
        conclusion,
        completed_at: new Date().toISOString(),
        output: {
          title: conclusion === "success" ? "No GauntletCI findings" : "GauntletCI findings detected",
          summary: truncate(summary, 65000),
        },
      }),
    }
  );
}

async function postIssueComment(repository: GitHubRepo, issueNumber: number, body: string, token: string): Promise<void> {
  await githubFetch(
    `https://api.github.com/repos/${repository.full_name}/issues/${issueNumber}/comments`,
    {
      method: "POST",
      headers: {
        Authorization: `Bearer ${token}`,
        "Content-Type": "application/json",
      },
      body: JSON.stringify({ body: truncate(body, 65000) }),
    }
  );
}

async function cloneRepository(repository: GitHubRepo, headSha: string, repoDir: string, token: string): Promise<void> {
  const authHeader = Buffer.from(`x-access-token:${token}`).toString("base64");
  await runProcess("git", [
    "-c",
    `http.https://github.com/.extraheader=AUTHORIZATION: basic ${authHeader}`,
    "clone",
    "--no-checkout",
    "--filter=blob:none",
    repository.clone_url,
    repoDir,
  ]);
  await runProcess("git", ["checkout", headSha], { cwd: repoDir });
}

async function runGauntletCI(diffPath: string, repoDir: string): Promise<{ exitCode: number; stdout: string; stderr: string }> {
  return runProcess(env.gauntletciCommand, [
    "analyze",
    "--diff",
    diffPath,
    "--repo",
    repoDir,
    "--output",
    "json",
    "--no-banner",
    "--severity",
    env.severity,
    "--sensitivity",
    env.sensitivity,
  ], { allowNonZeroExit: true });
}

async function runProcess(
  command: string,
  args: string[],
  options: { cwd?: string; allowNonZeroExit?: boolean } = {}
): Promise<{ exitCode: number; stdout: string; stderr: string }> {
  return new Promise((resolvePromise, reject) => {
    const child = spawn(command, args, {
      cwd: options.cwd,
      shell: false,
      windowsHide: true,
    });

    let stdout = "";
    let stderr = "";
    child.stdout.on("data", (chunk: Buffer) => { stdout += chunk.toString("utf8"); });
    child.stderr.on("data", (chunk: Buffer) => { stderr += chunk.toString("utf8"); });
    child.on("error", reject);
    child.on("close", (code) => {
      const exitCode = code ?? 1;
      if (exitCode !== 0 && !options.allowNonZeroExit) {
        reject(new Error(`${command} exited with ${exitCode}: ${stderr}`));
        return;
      }
      resolvePromise({ exitCode, stdout, stderr });
    });
  });
}

function parseGauntletResult(stdout: string): GauntletResult {
  const trimmed = stdout.trim();
  if (!trimmed) return {};
  try {
    return JSON.parse(trimmed) as GauntletResult;
  } catch {
    const firstJson = trimmed.indexOf("{");
    const lastJson = trimmed.lastIndexOf("}");
    if (firstJson >= 0 && lastJson > firstJson) {
      return JSON.parse(trimmed.slice(firstJson, lastJson + 1)) as GauntletResult;
    }
    throw new Error("GauntletCI did not emit parseable JSON");
  }
}

function getFindings(result: GauntletResult): Finding[] {
  return result.Findings ?? result.findings ?? [];
}

function buildSummary(
  input: { repository: GitHubRepo; pullNumber: number; headSha: string; trigger: string },
  findings: Finding[],
  exitCode: number
): string {
  const topFindings = findings.slice(0, 20);
  const findingRows = topFindings.map((finding) => {
    const rule = finding.RuleId ?? finding.ruleId ?? finding.Rule ?? "GCI";
    const title = finding.Title ?? finding.title ?? finding.Message ?? finding.message ?? "Finding";
    const path = finding.FilePath ?? finding.filePath ?? finding.Path ?? finding.path ?? "";
    const line = finding.Line ?? finding.line;
    const location = path ? `${path}${line ? `:${line}` : ""}` : "No location";
    return `| ${escapeTable(rule)} | ${escapeTable(String(finding.Severity ?? finding.severity ?? ""))} | ${escapeTable(location)} | ${escapeTable(title)} |`;
  }).join("\n");

  return [
    "## GauntletCI Review",
    "",
    `GauntletCI completed for \`${input.repository.full_name}#${input.pullNumber}\`.`,
    "",
    `- Trigger: \`${input.trigger}\``,
    `- Commit: \`${input.headSha}\``,
    `- Findings: \`${findings.length}\``,
    `- Tool exit code: \`${exitCode}\``,
    `- Sensitivity: \`${env.sensitivity}\``,
    `- Severity threshold: \`${env.severity}\``,
    "",
    findings.length === 0
      ? "No findings were reported."
      : [
          "Top findings:",
          "",
          "| Rule | Severity | Location | Summary |",
          "| --- | --- | --- | --- |",
          findingRows,
          findings.length > topFindings.length ? `\nShowing ${topFindings.length} of ${findings.length} findings.` : "",
        ].join("\n"),
  ].join("\n");
}

function buildFailureSummary(input: { repository: GitHubRepo; pullNumber: number; trigger: string }, error: unknown): string {
  const message = error instanceof Error ? error.message : String(error);
  return [
    "## GauntletCI Review Failed",
    "",
    `GauntletCI failed for \`${input.repository.full_name}#${input.pullNumber}\`.`,
    "",
    `- Trigger: \`${input.trigger}\``,
    "",
    "```text",
    truncate(message, 2000),
    "```",
  ].join("\n");
}

async function githubFetch(url: string, init: RequestInit): Promise<Response> {
  const response = await fetch(url, {
    ...init,
    headers: {
      Accept: "application/vnd.github+json",
      "User-Agent": "GauntletCI-GitHub-App",
      "X-GitHub-Api-Version": "2022-11-28",
      ...init.headers,
    },
  });

  if (!response.ok) {
    const body = await response.text();
    throw new Error(`GitHub API ${response.status} ${response.statusText}: ${body}`);
  }

  return response;
}

function verifyGitHubSignature(body: Buffer, signatureHeader: string, secret: string): boolean {
  if (!signatureHeader.startsWith("sha256=")) return false;
  const actual = Buffer.from(signatureHeader.slice("sha256=".length), "hex");
  const expected = createHmac("sha256", secret).update(body).digest();
  return actual.length === expected.length && timingSafeEqual(actual, expected);
}

function createGitHubAppJwt(): string {
  const now = Math.floor(Date.now() / 1000);
  const header = base64UrlJson({ alg: "RS256", typ: "JWT" });
  const payload = base64UrlJson({
    iat: now - 60,
    exp: now + 9 * 60,
    iss: env.appId,
  });
  const data = `${header}.${payload}`;
  const key = createPrivateKey(normalizePrivateKey(env.privateKey));
  const signature = createSign("RSA-SHA256").update(data).end().sign(key);
  return `${data}.${base64Url(signature)}`;
}

function requiredInstallationId(installation?: GitHubInstallation): number {
  if (!installation?.id) throw new Error("Webhook payload did not include installation.id");
  return installation.id;
}

function readRequestBody(request: IncomingMessage): Promise<Buffer> {
  return new Promise((resolvePromise, reject) => {
    const chunks: Buffer[] = [];
    request.on("data", (chunk: Buffer) => chunks.push(chunk));
    request.on("end", () => resolvePromise(Buffer.concat(chunks)));
    request.on("error", reject);
  });
}

function parseJson(body: Buffer): unknown | null {
  try {
    return JSON.parse(body.toString("utf8")) as unknown;
  } catch {
    return null;
  }
}

function sendText(response: ServerResponse, statusCode: number, body: string): void {
  response.statusCode = statusCode;
  response.setHeader("Content-Type", "text/plain; charset=utf-8");
  response.end(body);
}

function loadEnv(): Env {
  const required = ["GITHUB_APP_ID", "GITHUB_PRIVATE_KEY", "GITHUB_WEBHOOK_SECRET"];
  for (const name of required) {
    if (!process.env[name]) throw new Error(`Missing required environment variable ${name}`);
  }

  return {
    appId: process.env.GITHUB_APP_ID!,
    privateKey: process.env.GITHUB_PRIVATE_KEY!,
    webhookSecret: process.env.GITHUB_WEBHOOK_SECRET!,
    port: parseInt(process.env.PORT ?? "8787", 10),
    gauntletciCommand: process.env.GAUNTLETCI_COMMAND ?? "gauntletci",
    sensitivity: process.env.GAUNTLETCI_SENSITIVITY ?? "permissive",
    severity: process.env.GAUNTLETCI_SEVERITY ?? "info",
    autoReviewPullRequests: (process.env.GAUNTLETCI_AUTO_REVIEW_PULL_REQUESTS ?? "false").toLowerCase() === "true",
    workDir: process.env.GAUNTLETCI_WORK_DIR ?? ".gauntletci-github-app-work",
  };
}

function normalizePrivateKey(privateKey: string): string {
  return privateKey.replace(/\\n/g, "\n");
}

function base64UrlJson(value: unknown): string {
  return base64Url(Buffer.from(JSON.stringify(value), "utf8"));
}

function base64Url(value: Buffer): string {
  return value.toString("base64url");
}

function escapeTable(value: string): string {
  return value.replace(/\|/g, "\\|").replace(/\r?\n/g, " ");
}

function truncate(value: string, maxLength: number): string {
  return value.length <= maxLength ? value : `${value.slice(0, maxLength - 20)}\n\n[truncated]`;
}
