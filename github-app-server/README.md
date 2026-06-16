# GauntletCI GitHub App Server

This is the backend for the GauntletCI GitHub App.

It receives GitHub App webhooks, verifies GitHub signatures, authenticates as the installed GitHub App, fetches pull request diffs, runs the `gauntletci` CLI on the API host, and posts a GitHub check plus PR summary comment.

## MVP behavior

- `GET /healthz` returns `OK`.
- `POST /github/webhook` handles GitHub App webhooks.
- `ping` events return `200`.
- `issue_comment` events run when a PR comment contains `@gauntletci review`.
- `pull_request` events can run on `opened`, `synchronize`, and `reopened` when `GAUNTLETCI_AUTO_REVIEW_PULL_REQUESTS=true`.
- Results are posted as:
  - a check run named `GauntletCI`
  - a PR comment with collapsible finding details (evidence, why it matters, suggested action)
  - inline PR review comments on diff lines via `--github-pr-comments` (same format as the GitHub Action)

Inline comments use the same `GitHubPrReviewWriter` path as the CLI. Findings outside the diff appear only in the summary comment.

## Required host tools

The host must have:

- Node.js 20 or later
- Git
- .NET SDK/runtime needed by the GauntletCI global tool
- `gauntletci` available on `PATH`

Install GauntletCI on the host:

```bash
dotnet tool install -g GauntletCI --version 2.8.1
```

## Required GitHub App permissions

- Contents: read
- Pull requests: write
- Checks: write
- Issues: write
- Metadata: read

Required events:

- `ping`
- `issue_comment`
- `pull_request`

## Environment

Copy `.env.example` to your host secret configuration and set:

```bash
GITHUB_APP_ID=3847577
GITHUB_PRIVATE_KEY='-----BEGIN RSA PRIVATE KEY----- ...'
GITHUB_WEBHOOK_SECRET='...'
PORT=8787
```

If your private key is stored with literal `\n` sequences, the server normalizes them at runtime.

## Run locally

```bash
npm install
npm run check
npm run dev
```

Point the GitHub App webhook URL at:

```text
https://your-host.example.com/github/webhook
```

## Trigger a review

On a pull request in a repository where the app is installed, comment:

```text
@gauntletci review
```

The app will fetch the PR diff, clone the repository at the PR head SHA into a temporary working directory, run:

```bash
gauntletci analyze --diff pr.diff --repo repo --output json --no-banner --github-pr-comments --severity info --sensitivity permissive
```

Then it will post a check run, inline review comments where lines are in the diff, and a detailed summary comment back to the PR.

## Deploy on Railway

Create a new Railway project from this GitHub repository and set the service root to:

```text
github-app-server
```

Railway should detect and build the included `Dockerfile`.

Set these Railway variables:

```text
GITHUB_APP_ID=3847577
GITHUB_PRIVATE_KEY=<contents of the GitHub App private key PEM>
GITHUB_WEBHOOK_SECRET=<the GitHub App webhook secret>
PORT=8787
GAUNTLETCI_COMMAND=gauntletci
GAUNTLETCI_SENSITIVITY=permissive
GAUNTLETCI_SEVERITY=info
GAUNTLETCI_AUTO_REVIEW_PULL_REQUESTS=false
GAUNTLETCI_WORK_DIR=.gauntletci-github-app-work
```

After Railway deploys, copy the public service URL and configure the GitHub App webhook URL:

```text
https://<railway-host>/github/webhook
```

Then use GitHub's **Recent Deliveries** panel for the app webhook and verify the `ping` delivery returns `200`.
