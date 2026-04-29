# GauntletCI + husky

[husky](https://typicode.github.io/husky/) is the most popular git hooks manager for
JavaScript and TypeScript projects. This guide wires GauntletCI into a `pre-commit` hook
for repos that use a Node.js toolchain alongside .NET code (monorepos, full-stack apps).

## Prerequisites

- .NET 8+ SDK
- GauntletCI CLI: `dotnet tool install -g GauntletCI`
- husky: `npm install --save-dev husky`

## Setup

### 1. Initialize husky

```bash
npx husky init
```

This creates `.husky/pre-commit` with a sample hook.

### 2. Replace the pre-commit hook

`.husky/pre-commit`:

```sh
#!/bin/sh
gauntletci analyze --sensitivity balanced
```

That is all that is needed. GauntletCI exits 1 on block-level findings, which
causes husky to abort the commit.

### 3. Make it configurable per developer

If team members run different sensitivity levels, use an env var:

```sh
#!/bin/sh
SENSITIVITY="${GAUNTLETCI_SENSITIVITY:-balanced}"
gauntletci analyze --sensitivity "$SENSITIVITY"
```

### 4. Verify

```bash
git add .
git commit -m "test: verify GauntletCI hook"
```

### 5. Share with the team

Add the `prepare` script to `package.json` so the hook installs for every developer
after `npm install`:

```json
{
  "scripts": {
    "prepare": "husky"
  }
}
```

## Skipping the hook

Individual developers can skip the hook for a commit when needed:

```bash
HUSKY=0 git commit -m "..."
```

## Links

- [husky docs](https://typicode.github.io/husky/)
- [GauntletCI CLI reference](https://gauntletci.com/docs/cli)
- [GauntletCI configuration](https://gauntletci.com/docs/configuration)
