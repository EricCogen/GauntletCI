// SPDX-License-Identifier: Elastic-2.0
using GauntletCI.Core.Analysis;
using GauntletCI.Core.Diff;
using GauntletCI.Core.Model;
using GauntletCI.Core.Rules.Implementations;

namespace GauntletCI.Tests.Rules;

public class GCI0003Tests
{
    private static readonly GCI0003_BehavioralChangeDetection Rule = new();

    private static AnalysisContext MakeContext(DiffContext diff) => new() { Diff = diff };

    [Fact]
    public async Task RemovedLogicWithoutTests_ShouldFlag()
    {
        // 17 removed lines all containing explicit control-flow keywords: well above
        // the threshold (15). Represents a whole validation method body being deleted.
        var raw = """
            diff --git a/src/Validator.cs b/src/Validator.cs
            index abc..def 100644
            --- a/src/Validator.cs
            +++ b/src/Validator.cs
            @@ -1,22 +1,4 @@
             public class Validator {
            -    public bool Validate(Order order) {
            -        if (order == null) throw new ArgumentNullException(nameof(order));
            -        if (order.Amount <= 0) return false;
            -        if (order.Amount > 10000) return false;
            -        if (string.IsNullOrEmpty(order.CustomerId)) return false;
            -        if (order.Items.Count == 0) return false;
            -        if (order.Items.Any(i => i.Quantity <= 0)) return false;
            -        if (order.Items.Any(i => i.Price < 0)) return false;
            -        if (order.Currency != "USD") return false;
            -        if (order.ShippingAddress == null) throw new ArgumentException("required");
            -        if (order.ShippingAddress.ZipCode.Length < 5) return false;
            -        if (order.ShippingAddress.Country == null) return false;
            -        if (order.DeliveryDate < DateTime.UtcNow) return false;
            -        if (order.DeliveryDate > DateTime.UtcNow.AddYears(1)) return false;
            -        if (order.Notes != null && order.Notes.Length > 500) return false;
            -        return true;
            -    }
             }
            """;

        var diff = DiffParser.Parse(raw);
        var findings = await Rule.EvaluateAsync(MakeContext(diff), default);

        Assert.Contains(findings, f => f.Summary.Contains("logic line(s) removed"));
    }

    [Fact]
    public async Task SmallLogicRemoval_ShouldNotFlag()
    {
        // Only 5 removed logic lines: routine refactor, below the 15-line threshold.
        var raw = """
            diff --git a/src/Service.cs b/src/Service.cs
            index abc..def 100644
            --- a/src/Service.cs
            +++ b/src/Service.cs
            @@ -1,9 +1,3 @@
             public int Compute(int x) {
            -    if (x < 0) throw new ArgumentException("negative");
            -    if (x == 0) return 0;
            -    if (x > 1000) throw new OverflowException("too large");
            -    if (x % 2 == 0) return x / 2;
            -    return x * 2;
             }
            """;

        var diff = DiffParser.Parse(raw);
        var findings = await Rule.EvaluateAsync(MakeContext(diff), default);

        Assert.DoesNotContain(findings, f => f.Summary.Contains("logic line(s) removed"));
    }

    [Fact]
    public async Task ManyFilesWithSigChanges_ShouldCollapseToSingleFinding()
    {
        // 4 files each changing the same public method sig - should collapse to one summary.
        static string FileBlock(string name) => $"""
            diff --git a/src/{name}.cs b/src/{name}.cs
            index abc..def 100644
            --- a/src/{name}.cs
            +++ b/src/{name}.cs
            @@ -1,3 +1,3 @@
             // class
            -public void DoWork(int x)
            +public void DoWork(int x, string label)
             // end
            """;

        var raw = string.Join("\n", new[] { "Alpha", "Beta", "Gamma", "Delta" }.Select(FileBlock));
        var diff = DiffParser.Parse(raw);
        var findings = await Rule.EvaluateAsync(MakeContext(diff), default);

        var f = Assert.Single(findings, x => x.Summary.Contains("signatures changed"));
        Assert.Contains("4 files", f.Summary);
    }
    [Fact]
    public async Task ChangedMethodSignature_ShouldFlag()
    {
        var raw = """
            diff --git a/src/Service.cs b/src/Service.cs
            index abc..def 100644
            --- a/src/Service.cs
            +++ b/src/Service.cs
            @@ -1,3 +1,3 @@
             // service class
            -public void DoWork(int x)
            +public void DoWork(int x, string y)
            """;

        var diff = DiffParser.Parse(raw);
        var findings = await Rule.EvaluateAsync(MakeContext(diff), default);

        Assert.Contains(findings, f => f.Summary.Contains("signature changed"));
    }

    [Fact]
    public async Task PrivateMethodSignatureChange_ShouldNotFlag()
    {
        var raw = """
            diff --git a/src/Service.cs b/src/Service.cs
            index abc..def 100644
            --- a/src/Service.cs
            +++ b/src/Service.cs
            @@ -1,2 +1,2 @@
            -private void Helper(int x)
            +private void Helper(int x, string y)
            """;

        var diff = DiffParser.Parse(raw);
        var findings = await Rule.EvaluateAsync(MakeContext(diff), default);

        Assert.DoesNotContain(findings, f => f.Summary.Contains("signature changed"));
    }

    [Fact]
    public async Task PublicMethodOnlyOptionalParamsAdded_ShouldFlagAsLowConfidence()
    {
        var raw = """
            diff --git a/src/Service.cs b/src/Service.cs
            index abc..def 100644
            --- a/src/Service.cs
            +++ b/src/Service.cs
            @@ -1,2 +1,2 @@
            -public void DoWork(int x)
            +public void DoWork(int x, string label = "default", bool verbose = false)
            """;

        var diff = DiffParser.Parse(raw);
        var findings = await Rule.EvaluateAsync(MakeContext(diff), default);

        var f = Assert.Single(findings, f => f.Summary.Contains("Backward-compatible", StringComparison.Ordinal));
        Assert.Equal(Confidence.Low, f.Confidence);
    }

    [Fact]
    public async Task PublicMethodRequiredParamAdded_ShouldFlagAsMediumConfidence()
    {
        var raw = """
            diff --git a/src/Service.cs b/src/Service.cs
            index abc..def 100644
            --- a/src/Service.cs
            +++ b/src/Service.cs
            @@ -1,2 +1,2 @@
            -public void DoWork(int x)
            +public void DoWork(int x, string y)
            """;

        var diff = DiffParser.Parse(raw);
        var findings = await Rule.EvaluateAsync(MakeContext(diff), default);

        var f = Assert.Single(findings, f => f.Summary.Contains("signature changed"));
        Assert.Equal(Confidence.Medium, f.Confidence);
    }

    [Fact]
    public async Task RemovedLogicWithTestChanges_ShouldNotFlag()
    {
        var raw = """
            diff --git a/src/Service.cs b/src/Service.cs
            index abc..def 100644
            --- a/src/Service.cs
            +++ b/src/Service.cs
            @@ -1,3 +1,2 @@
             public int Compute() {
            -    return x * 2;
             }
            diff --git a/src/ServiceTests.cs b/src/ServiceTests.cs
            index abc..def 100644
            --- a/src/ServiceTests.cs
            +++ b/src/ServiceTests.cs
            @@ -1,1 +1,2 @@
             // test
            +Assert.Equal(0, svc.Compute());
            """;

        var diff = DiffParser.Parse(raw);
        var findings = await Rule.EvaluateAsync(MakeContext(diff), default);

        Assert.DoesNotContain(findings, f => f.Summary.Contains("logic line(s) removed"));
    }

    [Fact]
    public async Task ExpressionBodyChange_ShouldNotFlagSignatureChange()
    {
        // Only the expression body changes: the signature (name + params) is identical.
        var raw = """
            diff --git a/src/Core/Checker.cs b/src/Core/Checker.cs
            index abc..def 100644
            --- a/src/Core/Checker.cs
            +++ b/src/Core/Checker.cs
            @@ -1,3 +1,3 @@
             public class Checker {
            -    public bool HasModifier(string content) => content.Contains("internal ");
            +    public bool HasModifier(string content) => content.Contains("internal ", StringComparison.Ordinal);
             }
            """;

        var diff = DiffParser.Parse(raw);
        var findings = await Rule.EvaluateAsync(MakeContext(diff), default);

        Assert.DoesNotContain(findings, f => f.Summary.Contains("signature changed"));
    }

    [Fact]
    public async Task AccessModifierInStringLiteralWithParen_ShouldNotFlagSignatureChange()
    {
        // A non-signature line containing "internal " in a string plus "(" should not
        // be treated as a method signature (exercises TrimStart().StartsWith() guard).
        var raw = """
            diff --git a/src/Core/Checker.cs b/src/Core/Checker.cs
            index abc..def 100644
            --- a/src/Core/Checker.cs
            +++ b/src/Core/Checker.cs
            @@ -1,3 +1,3 @@
             public class Checker {
            -    var msg = "internal method(old)";
            +    var msg = "internal method(new)";
             }
            """;

        var diff = DiffParser.Parse(raw);
        var findings = await Rule.EvaluateAsync(MakeContext(diff), default);

        Assert.DoesNotContain(findings, f => f.Summary.Contains("signature changed"));
    }

    [Fact]
    public async Task AttributeDecoratedMethod_ShouldFlagSignatureChange()
    {
        // [Obsolete] attribute before "public" should not prevent signature detection.
        var raw = """
            diff --git a/src/Core/Api.cs b/src/Core/Api.cs
            index abc..def 100644
            --- a/src/Core/Api.cs
            +++ b/src/Core/Api.cs
            @@ -1,3 +1,3 @@
             public class Api {
            -    [Obsolete] public void Process(string input) { }
            +    [Obsolete] public void Process(string input, int timeout) { }
             }
            """;

        var diff = DiffParser.Parse(raw);
        var findings = await Rule.EvaluateAsync(MakeContext(diff), default);

        Assert.Contains(findings, f => f.Summary.Contains("signature changed"));
    }

    [Fact]
    public async Task GenericConstraintChange_ShouldFlagSignatureChange()
    {
        // A where-clause change is a signature-level breaking change and must be flagged.
        var raw = """
            diff --git a/src/Core/Api.cs b/src/Core/Api.cs
            index abc..def 100644
            --- a/src/Core/Api.cs
            +++ b/src/Core/Api.cs
            @@ -1,3 +1,3 @@
             public class Api {
            -    public void Process<T>(T input) where T : struct { }
            +    public void Process<T>(T input) where T : class { }
             }
            """;

        var diff = DiffParser.Parse(raw);
        var findings = await Rule.EvaluateAsync(MakeContext(diff), default);

        Assert.Contains(findings, f => f.Summary.Contains("signature changed"));
    }

    [Fact]
    public async Task CryptographicMethodArgumentChange_ShouldFlagBoundaryChange()
    {
        // .NET 10.0.7 HMAC validation vulnerability: arguments to ComputeHash changed
        // from entire payload to payload with first 16 bytes skipped.
        var raw = """
            diff --git a/src/DataProtection.cs b/src/DataProtection.cs
            index abc..def 100644
            --- a/src/DataProtection.cs
            +++ b/src/DataProtection.cs
            @@ -1,3 +1,3 @@
            -var tag = _hmac.ComputeHash(ciphertext);
            +var tag = _hmac.ComputeHash(ciphertext.Skip(16).ToArray());
             return result;
            """;

        var diff = DiffParser.Parse(raw);
        var findings = await Rule.EvaluateAsync(MakeContext(diff), default);

        var f = Assert.Single(findings, x => x.Summary.Contains("Cryptographic method"));
        Assert.Equal(Confidence.High, f.Confidence);
        Assert.Contains("ComputeHash", f.Summary);
        Assert.Contains("different arguments", f.Summary);
    }

    [Fact]
    public async Task CryptographicMethodDecryptArgumentChange_ShouldFlagBoundaryChange()
    {
        // Changes to Decrypt method arguments also represent security boundary changes.
        var raw = """
            diff --git a/src/Cipher.cs b/src/Cipher.cs
            index abc..def 100644
            --- a/src/Cipher.cs
            +++ b/src/Cipher.cs
            @@ -1,5 +1,5 @@
             public class Cipher {
            -    byte[] plaintext = _aes.Decrypt(ciphertext, iv);
            +    byte[] plaintext = _aes.Decrypt(ciphertext.Skip(1), iv);
                 return plaintext;
             }
            """;

        var diff = DiffParser.Parse(raw);
        var findings = await Rule.EvaluateAsync(MakeContext(diff), default);

        var f = Assert.Single(findings, x => x.Summary.Contains("Cryptographic method"));
        Assert.Equal(Confidence.High, f.Confidence);
    }

    [Fact]
    public async Task MultipleCryptographicMethodsChanged_ShouldFlagEach()
    {
        // Multiple cryptographic methods changed in the same diff should each be flagged.
        var raw = """
            diff --git a/src/Cipher.cs b/src/Cipher.cs
            index abc..def 100644
            --- a/src/Cipher.cs
            +++ b/src/Cipher.cs
            @@ -1,7 +1,7 @@
             public class Cipher {
            -    var hmac1 = _alg.ComputeHash(data);
            +    var hmac1 = _alg.ComputeHash(data.Skip(1).ToArray());
             public void Method2() {
            -    var sig = _key.Sign(message);
            +    var sig = _key.Sign(message.Substring(1));
                 }
             }
            """;

        var diff = DiffParser.Parse(raw);
        var findings = await Rule.EvaluateAsync(MakeContext(diff), default);

        Assert.True(findings.Count(x => x.Summary.Contains("Cryptographic method")) >= 1);
    }

    [Fact]
    public async Task CryptographicMethodInTestFile_ShouldNotFlag()
    {
        // Changes to cryptographic methods in test files are not flagged (tests can change logic freely).
        var raw = """
            diff --git a/tests/DataProtectionTests.cs b/tests/DataProtectionTests.cs
            index abc..def 100644
            --- a/tests/DataProtectionTests.cs
            +++ b/tests/DataProtectionTests.cs
            @@ -1,3 +1,3 @@
             public class Tests {
            -    var result1 = _hmac.ComputeHash(testPayload);
            +    var result2 = _hmac.ComputeHash(testPayload.Skip(4).ToArray());
             }
            """;

        var diff = DiffParser.Parse(raw);
        var findings = await Rule.EvaluateAsync(MakeContext(diff), default);

        Assert.DoesNotContain(findings, f => f.Summary.Contains("Cryptographic method"));
    }
}


