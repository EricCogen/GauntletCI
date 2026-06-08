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
    exit 0
fi

echo "GauntletCI: Analyzing staged changes..."

# Determine if jq is available
HAS_JQ=0
if command -v jq &> /dev/null; then
    HAS_JQ=1
fi

# Run gauntletci — JSON output uses Confidence: 0=Low, 1=Medium, 2=High
OUTPUT=$(run_gauntletci analyze --staged --output json --no-banner 2>&1) || {
    echo "GauntletCI failed to run. Commit aborted."
    echo "$OUTPUT"
    exit 1
}

# Count findings by confidence level
if [ $HAS_JQ -eq 1 ]; then
    HIGH_COUNT=$(echo "$OUTPUT" | jq '[.Findings[] | select(.Confidence == 2)] | length')
    MEDIUM_COUNT=$(echo "$OUTPUT" | jq '[.Findings[] | select(.Confidence == 1)] | length')
    LOW_COUNT=$(echo "$OUTPUT" | jq '[.Findings[] | select(.Confidence == 0)] | length')
else
    HIGH_COUNT=$(echo "$OUTPUT" | grep -c '"Confidence": 2' || true)
    MEDIUM_COUNT=$(echo "$OUTPUT" | grep -c '"Confidence": 1' || true)
    LOW_COUNT=$(echo "$OUTPUT" | grep -c '"Confidence": 0' || true)
    HIGH_COUNT="${HIGH_COUNT:-0}"
    MEDIUM_COUNT="${MEDIUM_COUNT:-0}"
    LOW_COUNT="${LOW_COUNT:-0}"
fi

TOTAL=$((HIGH_COUNT + MEDIUM_COUNT + LOW_COUNT))

if [ "$HIGH_COUNT" -gt 0 ]; then
    echo ""
    echo "GauntletCI found $HIGH_COUNT high-confidence issue(s):"
    if [ $HAS_JQ -eq 1 ]; then
        echo "$OUTPUT" | jq -r '.Findings[] | select(.Confidence == 2) | "  • \u001b[31m[\(.RuleId)]\u001b[0m \(.Summary)\n    \(.Evidence)"'
    else
        echo "$OUTPUT" | grep -A 5 '"Confidence": 2' | head -30
    fi
    echo ""
    echo "Commit aborted. Fix high-confidence issues or use --no-verify to bypass."
    exit 1
elif [ "$TOTAL" -gt 0 ]; then
    echo ""
    echo "GauntletCI found $TOTAL issue(s) (none high-confidence):"
    if [ $HAS_JQ -eq 1 ]; then
        echo "$OUTPUT" | jq -r '.Findings[] | "  • [\(.RuleId)] \(.Summary)"'
    else
        echo "$OUTPUT"
    fi
    echo ""
    echo "Commit allowed, but consider reviewing."
else
    echo "GauntletCI found no issues."
fi
