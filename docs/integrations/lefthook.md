# GauntletCI + Lefthook

[Lefthook](https://github.com/evilmartians/lefthook) is a fast, cross-platform git hooks
manager with no Node.js dependency. It is a good fit for pure .NET repositories.

## Prerequisites

- .NET 8+ SDK
- GauntletCI CLI: `dotnet tool install -g GauntletCI`
- Lefthook: see [installation options](https://github.com/evilmartians/lefthook/blob/master/docs/install.md)

Common install methods:

```bash
# via npm
npm install --save-dev lefthook

# via Homebrew (macOS/Linux)
brew install lefthook

# via Scoop (Windows)
scoop install lefthook
```

## Setup

### 1. Initialize

```bash
lefthook install
```

### 2. Configure `lefthook.yml`

Create `lefthook.yml` in the repository root:

```yaml
pre-commit:
  commands:
    gauntletci:
      run: gauntletci analyze --sensitivity balanced
      glob: "*.cs"
```

The `glob` filter means Lefthook only runs GauntletCI when `.cs` files are staged.
Remove it to run on every commit regardless of staged files.

### 3. Verify

```bash
lefthook run pre-commit
```

## Parallel execution

Lefthook supports running hooks in parallel. To run GauntletCI alongside other hooks:

```yaml
pre-commit:
  parallel: true
  commands:
    gauntletci:
      run: gauntletci analyze --sensitivity balanced
      glob: "*.cs"
    dotnet-format:
      run: dotnet format --verify-no-changes
      glob: "*.cs"
```

## Per-environment sensitivity

```yaml
pre-commit:
  commands:
    gauntletci:
      run: gauntletci analyze --sensitivity {GAUNTLETCI_SENSITIVITY:-balanced}
```

## Team setup

Commit `lefthook.yml` to the repository. Each developer runs `lefthook install` once
after cloning. You can automate this via a `dotnet restore` target or project `targets`
in your `.csproj`.

## Links

- [Lefthook docs](https://github.com/evilmartians/lefthook)
- [GauntletCI CLI reference](https://gauntletci.com/docs/cli)
- [GauntletCI configuration](https://gauntletci.com/docs/configuration)
