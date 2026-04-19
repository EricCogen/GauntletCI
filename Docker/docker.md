# Running GauntletCI with Docker

Pull the latest image from GHCR:

```bash
docker pull ghcr.io/ericcogen/gauntletci:latest
```

## Basic usage

Mount your repository into the container and run `analyze`:

```bash
docker run --rm \
  -v $(pwd):/repo \
  ghcr.io/ericcogen/gauntletci \
  analyze --path /repo
```

## With a config file

```bash
docker run --rm \
  -v $(pwd):/repo \
  ghcr.io/ericcogen/gauntletci \
  analyze --path /repo --config /repo/.gauntletci.json
```

## With GitHub PR review output

Pass environment variables for GitHub Actions integration:

```bash
docker run --rm \
  -v $(pwd):/repo \
  -e GITHUB_TOKEN=$GITHUB_TOKEN \
  -e GITHUB_REPOSITORY=$GITHUB_REPOSITORY \
  -e GITHUB_SHA=$GITHUB_SHA \
  ghcr.io/ericcogen/gauntletci \
  analyze --path /repo --output github-pr-review
```

## Available tags

| Tag | Description |
|-----|-------------|
| `latest` | Latest build from `main` |
| `sha-{short}` | Pinned to a specific commit |
