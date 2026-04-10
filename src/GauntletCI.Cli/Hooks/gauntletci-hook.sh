#!/bin/bash
# GauntletCI Pre-Commit Hook
# Installed by `gauntletci init`

set -e

# Resolve the gauntletci binary
if command -v gauntletci &> /dev/null; then
    GAUNTLETCI_CMD="gauntletci"
elif dotnet tool list -g 2>/dev/null | grep -qi "gauntletci"; then
    GAUNTLETCI_CMD="dotnet gauntletci"
else
    echo "⚠️  GauntletCI not found in PATH or as a global dotnet tool. Skipping pre-commit check."
    exit 0
fi

echo "🔍 GauntletCI: Analyzing staged changes..."

# Determine if jq is available
HAS_JQ=0
if command -v jq &> /dev/null; then
    HAS_JQ=1
fi

# Run gauntletci — JSON output uses Confidence: 0=Low, 1=Medium, 2=High
OUTPUT=$($GAUNTLETCI_CMD analyze --staged --output json --no-banner 2>&1) || {
    echo "❌ GauntletCI failed to run. Commit aborted."
    echo "$OUTPUT"
    exit 1
}

# Count findings by confidence level
if [ $HAS_JQ -eq 1 ]; then
    HIGH_COUNT=$(echo "$OUTPUT" | jq '[.Findings[] | select(.Confidence == 2)] | length')
    MEDIUM_COUNT=$(echo "$OUTPUT" | jq '[.Findings[] | select(.Confidence == 1)] | length')
    LOW_COUNT=$(echo "$OUTPUT" | jq '[.Findings[] | select(.Confidence == 0)] | length')
else
    HIGH_COUNT=$(echo "$OUTPUT" | grep -c '"Confidence": 2' || echo 0)
    MEDIUM_COUNT=$(echo "$OUTPUT" | grep -c '"Confidence": 1' || echo 0)
    LOW_COUNT=$(echo "$OUTPUT" | grep -c '"Confidence": 0' || echo 0)
fi

TOTAL=$((HIGH_COUNT + MEDIUM_COUNT + LOW_COUNT))

if [ "$HIGH_COUNT" -gt 0 ]; then
    echo ""
    echo "🚨 GauntletCI found $HIGH_COUNT high-confidence issue(s):"
    if [ $HAS_JQ -eq 1 ]; then
        echo "$OUTPUT" | jq -r '.Findings[] | select(.Confidence == 2) | "  • \u001b[31m[\(.RuleId)]\u001b[0m \(.Summary)\n    \(.Evidence)"'
    else
        echo "$OUTPUT" | grep -A 5 '"Confidence": 2' | head -30
    fi
    echo ""
    echo "❌ Commit aborted. Fix high-confidence issues or use --no-verify to bypass."
    exit 1
elif [ "$TOTAL" -gt 0 ]; then
    echo ""
    echo "⚠️  GauntletCI found $TOTAL issue(s) (none high-confidence):"
    if [ $HAS_JQ -eq 1 ]; then
        echo "$OUTPUT" | jq -r '.Findings[] | "  • [\(.RuleId)] \(.Summary)"'
    else
        echo "$OUTPUT"
    fi
    echo ""
    echo "✅ Commit allowed, but consider reviewing."
else
    echo "✅ GauntletCI found no issues."
fi