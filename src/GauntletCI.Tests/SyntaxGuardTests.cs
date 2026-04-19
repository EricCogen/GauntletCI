// SPDX-License-Identifier: Elastic-2.0
using GauntletCI.Core.Diff;
using GauntletCI.Core.Rules.Implementations;
using GauntletCI.Core.StaticAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;

namespace GauntletCI.Tests;

/// <summary>Tests for <see cref="SyntaxGuard"/> and <see cref="SyntaxContext"/>.</summary>
public class SyntaxGuardTests
{
    // ── SyntaxGuard.HasObjectCreation ──────────────────────────────────────

    [Fact]
    public void HasObjectCreation_WhenLineHasMatchingNew_ReturnsTrue()
    {
        var tree = Parse("var r = new Random();");
        Assert.True(SyntaxGuard.HasObjectCreation(tree, 1, "Random"));
    }

    [Fact]
    public void HasObjectCreation_WhenLineHasDifferentType_ReturnsFalse()
    {
        var tree = Parse("var r = new RandomHelper();");
        Assert.False(SyntaxGuard.HasObjectCreation(tree, 1, "Random"));
    }

    [Fact]
    public void HasObjectCreation_WhenNewIsInsideComment_ReturnsFalse()
    {
        var source = "// use new Random() for testing\nvar x = 1;";
        var tree = Parse(source);
        Assert.False(SyntaxGuard.HasObjectCreation(tree, 1, "Random"));
    }

    [Fact]
    public void HasObjectCreation_WhenLineNumberOutOfRange_ReturnsFalse()
    {
        var tree = Parse("var x = 1;");
        Assert.False(SyntaxGuard.HasObjectCreation(tree, 999, "Random"));
        Assert.False(SyntaxGuard.HasObjectCreation(tree, 0, "Random"));
    }

    // ── SyntaxGuard.IsInCommentOrStringLiteral ────────────────────────────

    [Fact]
    public void IsInCommentOrString_WhenLineIsFullSingleLineComment_ReturnsTrue()
    {
        var source = "var x = 1;\n// new Random() is insecure\nvar y = 2;";
        var tree = Parse(source);
        // Column 0 is inside the '//' comment
        Assert.True(SyntaxGuard.IsInCommentOrStringLiteral(tree, 2, 0));
    }

    [Fact]
    public void IsInCommentOrString_WhenLineIsCode_ReturnsFalse()
    {
        var tree = Parse("var r = new Random();");
        // Column 8 is at 'new Random(' — live code
        Assert.False(SyntaxGuard.IsInCommentOrStringLiteral(tree, 1, 8));
    }

    [Fact]
    public void IsInCommentOrString_WhenLineIsStringLiteral_ReturnsTrue()
    {
        var tree = Parse("var s = \"new Random() is bad\";");
        // Column 8 is inside the string literal
        Assert.True(SyntaxGuard.IsInCommentOrStringLiteral(tree, 1, 8));
    }

    [Fact]
    public void IsInCommentOrString_WhenLineNumberOutOfRange_ReturnsFalse()
    {
        var tree = Parse("var x = 1;");
        Assert.False(SyntaxGuard.IsInCommentOrStringLiteral(tree, 0, 0));
        Assert.False(SyntaxGuard.IsInCommentOrStringLiteral(tree, 999, 0));
    }

    [Fact]
    public void IsInCommentOrString_WhenCodeHasAdjacentStringOnSameLine_ReturnsFalse()
    {
        // The float literal `0.0` is live code — the adjacent string "bad" must not cause suppression
        var tree = Parse("if (result == 0.0) throw new Exception(\"bad\");");
        // Column 12 is at '== 0.0' — live code
        Assert.False(SyntaxGuard.IsInCommentOrStringLiteral(tree, 1, 12));
    }

    // ── SyntaxContext pass-through semantics ──────────────────────────────

    [Fact]
    public void SyntaxContext_IsConfirmedObjectCreation_PassesThroughWhenNoTree()
    {
        var ctx = new SyntaxContext(new Dictionary<string, Microsoft.CodeAnalysis.SyntaxTree>());
        // No tree for this file — should pass through (return true, don't suppress)
        Assert.True(ctx.IsConfirmedObjectCreation("src/Foo.cs", 1, "Random"));
    }

    [Fact]
    public void SyntaxContext_IsInCommentOrString_DoesNotSuppressWhenNoTree()
    {
        var ctx = new SyntaxContext(new Dictionary<string, Microsoft.CodeAnalysis.SyntaxTree>());
        // No tree — should NOT suppress (return false)
        Assert.False(ctx.IsInCommentOrStringLiteral("src/Foo.cs", 1, 0));
    }

    [Fact]
    public void SyntaxContext_ResolvesTreeBySuffixPath()
    {
        var tree = Parse("// comment line");
        var ctx = new SyntaxContext(new Dictionary<string, Microsoft.CodeAnalysis.SyntaxTree>
        {
            [@"C:\repo\src\Foo.cs"] = tree
        });
        Assert.True(ctx.IsInCommentOrStringLiteral("src/Foo.cs", 1, 0));
    }

    // ── Integration: GCI0048 suppressed by syntax guard ───────────────────

    [Fact]
    public async Task GCI0048_SuppressesNewRandomInSingleLineComment()
    {
        const string addedLine = "// var token = new Random(seed).Next(); // old insecure approach";
        // Parse with the same preceding "// existing" line so line 2 matches the diff's line 2
        var tree = Parse("// existing\n" + addedLine);
        var ctx  = new SyntaxContext(new Dictionary<string, Microsoft.CodeAnalysis.SyntaxTree>
        {
            ["src/Auth.cs"] = tree
        });

        var diff = MakeDiff("src/Auth.cs", addedLine);
        var rule = new GCI0048_InsecureRandomInSecurityContext();
        var findings = await rule.EvaluateAsync(diff, syntax: ctx);
        Assert.Empty(findings);
    }

    [Fact]
    public async Task GCI0048_FiresWhenNewRandomIsLiveCode()
    {
        const string addedLine = "    var token = new Random(seed).Next();";
        var diff = MakeDiff("src/Auth.cs", addedLine);
        var rule = new GCI0048_InsecureRandomInSecurityContext();
        var findings = await rule.EvaluateAsync(diff);
        Assert.NotEmpty(findings);
    }

    // ── Integration: GCI0049 suppressed by syntax guard ───────────────────

    [Fact]
    public async Task GCI0049_SuppressesFloatEqualityInsideComment()
    {
        const string addedLine = "// if (value == 0.0) return; // legacy check";
        var tree = Parse("// existing\n" + addedLine);
        var ctx  = new SyntaxContext(new Dictionary<string, Microsoft.CodeAnalysis.SyntaxTree>
        {
            ["src/Calc.cs"] = tree
        });

        var diff = MakeDiff("src/Calc.cs", addedLine);
        var rule = new GCI0049_FloatDoubleEqualityComparison();
        var findings = await rule.EvaluateAsync(diff, syntax: ctx);
        Assert.Empty(findings);
    }

    [Fact]
    public async Task GCI0049_FiresWhenFloatEqualityIsLiveCode()
    {
        const string addedLine = "    if (result == 0.0) throw new Exception();";
        var diff = MakeDiff("src/Calc.cs", addedLine);
        var rule = new GCI0049_FloatDoubleEqualityComparison();
        var findings = await rule.EvaluateAsync(diff);
        Assert.NotEmpty(findings);
    }

    // ─────────────────────────────────────────────────────────────────────

    private static Microsoft.CodeAnalysis.SyntaxTree Parse(string source) =>
        CSharpSyntaxTree.ParseText(SourceText.From(source));

    private static DiffContext MakeDiff(string filePath, string addedLine) =>
        DiffParser.Parse($"""
            diff --git a/{filePath} b/{filePath}
            index abc..def 100644
            --- a/{filePath}
            +++ b/{filePath}
            @@ -1,1 +1,2 @@
             // existing
            +{addedLine}
            """);
}

