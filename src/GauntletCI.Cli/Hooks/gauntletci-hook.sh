#!/bin/bash
# GauntletCI Pre-Commit Hook
# Installed by `gauntletci init`

set -e

resolve_gauntletci() {
    local current
    current="$(git rev-parse --show-toplevel 2>/dev/null || pwd)"
    while [ -n "$current" ] && [ "$current" != "/" ]; do
        if [ -f "$current/src/GauntletCI.Cli/GauntletCI.Cli.csproj" ]; then
            GAUNTLETCI_MODE="repo"
            GAUNTLETCI_REPO_ROOT="$current"
            return 0
        fi
        current="$(dirname "$current")"
    done

    if command -v gauntletci &> /dev/null; then
        GAUNTLETCI_MODE="shim"
        GAUNTLETCI_CMD="gauntletci"
        return 0
    fi

    local default_tools="$HOME/.dotnet/tools"
    local tools_dirs=()
    if [ -n "${DOTNET_ROOT:-}" ]; then
        tools_dirs+=("$DOTNET_ROOT/tools")
    fi
    tools_dirs+=("$default_tools")

    local dir
    for dir in "${tools_dirs[@]}"; do
        if [ -x "$dir/gauntletci" ]; then
            GAUNTLETCI_MODE="shim"
            GAUNTLETCI_CMD="$dir/gauntletci"
            return 0
        fi

        if [ -x "$dir/gauntletci.exe" ]; then
            GAUNTLETCI_MODE="shim"
            GAUNTLETCI_CMD="$dir/gauntletci.exe"
            return 0
        fi
    done

    return 1
}

run_gauntletci() {
    if [ "$GAUNTLETCI_MODE" = "repo" ]; then
        dotnet run --project "$GAUNTLETCI_REPO_ROOT/src/GauntletCI.Cli" -- "$@"
    else
        "$GAUNTLETCI_CMD" "$@"
    fi
}

GAUNTLETCI_MODE=""
GAUNTLETCI_CMD=""
GAUNTLETCI_REPO_ROOT=""
if ! resolve_gauntletci; then
    echo "GauntletCI not found. Install with: dotnet tool install -g GauntletCI"
    echo "Or from this repo: ./scripts/install-gauntletci-global-tool.ps1"
    if [ "${GAUNTLETCI_HOOK_OPTIONAL:-}" = "1" ]; then
        exit 0
    fi
    exit 1
fi

echo "GauntletCI: Analyzing staged changes..."

# Determine if jq is available
HAS_JQ=0
if command -v jq &> /dev/null; then
    HAS_JQ=1
fi

# Run gauntletci — JSON output uses Severity: 1=Info, 2=Warn, 3=Block
OUTPUT=$(run_gauntletci analyze --staged --output json --no-banner 2>&1) || {
    echo "GauntletCI failed to run. Commit aborted."
    echo "$OUTPUT"
    exit 1
}

# Count findings by severity level
if [ $HAS_JQ -eq 1 ]; then
    BLOCK_COUNT=$(echo "$OUTPUT" | jq '[.Findings[] | select(.Severity == 3)] | length')
    WARN_COUNT=$(echo "$OUTPUT" | jq '[.Findings[] | select(.Severity == 2)] | length')
    INFO_COUNT=$(echo "$OUTPUT" | jq '[.Findings[] | select(.Severity == 1)] | length')
else
    BLOCK_COUNT=$(echo "$OUTPUT" | grep -c '"Severity": 3' || true)
    WARN_COUNT=$(echo "$OUTPUT" | grep -c '"Severity": 2' || true)
    INFO_COUNT=$(echo "$OUTPUT" | grep -c '"Severity": 1' || true)
    BLOCK_COUNT="${BLOCK_COUNT:-0}"
    WARN_COUNT="${WARN_COUNT:-0}"
    INFO_COUNT="${INFO_COUNT:-0}"
fi

TOTAL=$((BLOCK_COUNT + WARN_COUNT + INFO_COUNT))

if [ "$BLOCK_COUNT" -gt 0 ]; then
    echo ""
    echo "GauntletCI found $BLOCK_COUNT Block-severity issue(s):"
    if [ $HAS_JQ -eq 1 ]; then
        echo "$OUTPUT" | jq -r '.Findings[] | select(.Severity == 3) | "  • \u001b[31m[\(.RuleId)]\u001b[0m \(.Summary)\n    \(.Evidence)"'
    else
        echo "$OUTPUT" | grep -A 5 '"Severity": 3' | head -30
    fi
    echo ""
    echo "Commit aborted. Fix Block-severity issues or use --no-verify to bypass."
    exit 1
elif [ "$TOTAL" -gt 0 ]; then
    echo ""
    echo "GauntletCI found $TOTAL issue(s) (Warn/Info only):"
    if [ $HAS_JQ -eq 1 ]; then
        echo "$OUTPUT" | jq -r '.Findings[] | select(.Severity == 2 or .Severity == 1) | "  • [\(.RuleId)] \(.Summary)"'
    else
        echo "$OUTPUT"
    fi
    echo ""
    echo "Commit allowed, but consider reviewing."
else
    echo "GauntletCI found no issues."
fi
