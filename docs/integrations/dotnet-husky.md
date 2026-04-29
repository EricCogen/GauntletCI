# GauntletCI + dotnet-husky

[dotnet-husky](https://alirezanet.github.io/Husky.Net/) is a .NET-native git hooks manager that
stores hooks in `.husky/` and installs them via `dotnet husky install`. This guide wires
GauntletCI into a pre-commit hook so every commit is analyzed before it lands.

## Prerequisites

- .NET 8+ SDK
- GauntletCI CLI: `dotnet tool install -g GauntletCI`
- dotnet-husky: `dotnet tool install -g Husky`

## Setup

### 1. Initialize dotnet-husky

```bash
cd your-repo
dotnet husky install
```

This creates `.husky/` and adds a `prepare` target to your project.

### 2. Add the pre-commit task

Create `.husky/task-runner.json` (or add to the existing one):

```json
{
  "tasks": [
    {
      "name": "GauntletCI",
      "command": "gauntletci",
      "args": ["analyze", "--sensitivity", "balanced"],
      "pathFilter": ["**/*.cs", "**/*.csproj", "**/*.sln", "**/*.slnx"],
      "output": "always"
    }
  ]
}
```

### 3. Run the task runner from the hook

`.husky/pre-commit`:

```bash
#!/bin/sh
. "$(dirname "$0")/_/husky.sh"
dotnet husky run --name GauntletCI
```

### 4. Verify

```bash
git commit --allow-empty -m "test: verify GauntletCI hook"
```

You should see GauntletCI output before the commit completes.

## Configuration

Any valid GauntletCI arguments work in `args`. Examples:

```json
"args": ["analyze", "--sensitivity", "strict"]
"args": ["analyze", "--sensitivity", "permissive", "--output", "text"]
```

To use a custom config file: `"args": ["analyze", "--config", ".gauntletci.json"]`

## Team setup

Add both `dotnet tool restore` and `dotnet husky install` to your CI setup step so
every developer gets the hook automatically after cloning:

```yaml
- run: dotnet tool restore
- run: dotnet husky install
```

Or add to a `.NET restore` target in your build script.

## Links

- [dotnet-husky docs](https://alirezanet.github.io/Husky.Net/)
- [GauntletCI CLI reference](https://gauntletci.com/docs/cli)
- [GauntletCI configuration](https://gauntletci.com/docs/configuration)
