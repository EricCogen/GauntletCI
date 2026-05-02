// SPDX-License-Identifier: Elastic-2.0
using GauntletCI.Core.Diff;
using GauntletCI.Core.Model;
using GauntletCI.Core.Rules.Implementations;

namespace GauntletCI.Tests.Rules;

public class GCI0006Tests
{
    private static readonly GCI0006_EdgeCaseHandling Rule = new();

    [Fact]
    public async Task ValueAccessWithoutNullGuard_ShouldFlag()
    {
        var raw = """
            diff --git a/src/Service.cs b/src/Service.cs
            index abc..def 100644
            --- a/src/Service.cs
            +++ b/src/Service.cs
            @@ -1,1 +1,2 @@
             // existing
            +var result = maybe.Value;
            """;

        var diff = DiffParser.Parse(raw);
        var findings = await Rule.EvaluateAsync(diff, null);

        Assert.Contains(findings, f => f.Summary.Contains("null dereference"));
    }

    [Fact]
    public async Task ValueAccessWithNullGuard_ShouldNotFlag()
    {
        // .HasValue check in a preceding added line
        var raw = """
            diff --git a/src/Service.cs b/src/Service.cs
            index abc..def 100644
            --- a/src/Service.cs
            +++ b/src/Service.cs
            @@ -1,1 +1,3 @@
             // existing
            +if (maybe.HasValue)
            +    var result = maybe.Value;
            """;

        var diff = DiffParser.Parse(raw);
        var findings = await Rule.EvaluateAsync(diff, null);

        Assert.DoesNotContain(findings, f => f.Summary.Contains("null dereference"));
    }

    [Fact]
    public async Task PublicMethodWithStringParam_ShouldFlag()
    {
        // Public method with string param, no null check in next lines
        var raw = """
            diff --git a/src/Processor.cs b/src/Processor.cs
            index abc..def 100644
            --- a/src/Processor.cs
            +++ b/src/Processor.cs
            @@ -1,1 +1,3 @@
             // existing
            +public void Process(string? input)
            +{
            """;

        var diff = DiffParser.Parse(raw);
        var findings = await Rule.EvaluateAsync(diff, null);

        Assert.Contains(findings, f => f.Summary.Contains("parameter(s) added"));
    }

    [Fact]
    public async Task PublicMethodWithStringParam_WithNullCheck_ShouldNotFlag()
    {
        var raw = """
            diff --git a/src/Processor.cs b/src/Processor.cs
            index abc..def 100644
            --- a/src/Processor.cs
            +++ b/src/Processor.cs
            @@ -1,1 +1,4 @@
             // existing
            +public void Process(string input)
            +{
            +    ArgumentNullException.ThrowIfNull(input);
            +}
            """;

        var diff = DiffParser.Parse(raw);
        var findings = await Rule.EvaluateAsync(diff, null);

        Assert.DoesNotContain(findings, f => f.Summary.Contains("parameter(s) added"));
    }

    [Fact]
    public async Task ValueInCommentLine_ShouldNotFlag()
    {
        // .Value inside a code comment: not executable
        var raw = """
            diff --git a/src/Service.cs b/src/Service.cs
            index abc..def 100644
            --- a/src/Service.cs
            +++ b/src/Service.cs
            @@ -1,1 +1,2 @@
             // existing
            +// match.Value shows the full attribute in findings
            """;

        var diff = DiffParser.Parse(raw);
        var findings = await Rule.EvaluateAsync(diff, null);

        Assert.DoesNotContain(findings, f => f.Summary.Contains("null dereference"));
    }

    [Fact]
    public async Task PrivateMethodWithStringParam_ShouldNotFlag()
    {
        // Private methods: callers are controlled, no need for null guards
        var raw = """
            diff --git a/src/Processor.cs b/src/Processor.cs
            index abc..def 100644
            --- a/src/Processor.cs
            +++ b/src/Processor.cs
            @@ -1,1 +1,3 @@
             // existing
            +private void Helper(string input)
            +{
            """;

        var diff = DiffParser.Parse(raw);
        var findings = await Rule.EvaluateAsync(diff, null);

        Assert.DoesNotContain(findings, f => f.Summary.Contains("parameter(s) added"));
    }

    [Fact]
    public async Task PublicMethodStringReturnType_NoStringParam_ShouldNotFlag()
    {
        // "string" in return type only: no string parameter
        var raw = """
            diff --git a/src/Processor.cs b/src/Processor.cs
            index abc..def 100644
            --- a/src/Processor.cs
            +++ b/src/Processor.cs
            @@ -1,1 +1,3 @@
             // existing
            +public string BuildBody(List<Finding> findings, bool hasInline)
            +{
            """;

        var diff = DiffParser.Parse(raw);
        var findings = await Rule.EvaluateAsync(diff, null);

        Assert.DoesNotContain(findings, f => f.Summary.Contains("parameter(s) added"));
    }

    [Fact]
    public async Task PublicMethodNonNullableStringParam_ShouldNotFlag()
    {
        // Non-nullable string annotation in nullable context; this rule does not require a guard here.
        var raw = """
            diff --git a/src/Processor.cs b/src/Processor.cs
            index abc..def 100644
            --- a/src/Processor.cs
            +++ b/src/Processor.cs
            @@ -1,1 +1,3 @@
             // existing
            +public void Process(string input)
            +{
            """;

        var diff = DiffParser.Parse(raw);
        var findings = await Rule.EvaluateAsync(diff, null);

        Assert.DoesNotContain(findings, f => f.Summary.Contains("parameter(s) added"));
    }

    [Fact]
    public async Task ValueAccessAfterSuccessGuard_ShouldNotFlag()
    {
        // match.Success is a valid null guard for match.Groups[1].Value
        var raw = """
            diff --git a/src/Parser.cs b/src/Parser.cs
            index abc..def 100644
            --- a/src/Parser.cs
            +++ b/src/Parser.cs
            @@ -1,1 +1,4 @@
             // existing
            +if (match.Success)
            +    return match.Groups[1].Value;
            +return string.Empty;
            """;

        var diff = DiffParser.Parse(raw);
        var findings = await Rule.EvaluateAsync(diff, null);

        Assert.DoesNotContain(findings, f => f.Summary.Contains("null dereference"));
    }

    [Fact]
    public async Task UnrelatedSuccessGuard_ShouldStillFlag()
    {
        // op.Success does not prove that nullable is non-null: should still flag
        var raw = """
            diff --git a/src/Parser.cs b/src/Parser.cs
            index abc..def 100644
            --- a/src/Parser.cs
            +++ b/src/Parser.cs
            @@ -1,1 +1,4 @@
             // existing
            +if (op.Success)
            +    return nullable.Value;
            +return string.Empty;
            """;

        var diff = DiffParser.Parse(raw);
        var findings = await Rule.EvaluateAsync(diff, null);

        Assert.Contains(findings, f => f.Summary.Contains("null dereference"));
    }

    [Fact]
    public async Task NullableInsideGenericParam_ShouldNotFlag()
    {
        // Dictionary<string?, int> is non-nullable at the top level: no guard needed
        var raw = """
            diff --git a/src/Processor.cs b/src/Processor.cs
            index abc..def 100644
            --- a/src/Processor.cs
            +++ b/src/Processor.cs
            @@ -1,1 +1,3 @@
             // existing
            +public void Process(Dictionary<string?, int> map)
            +{
            """;

        var diff = DiffParser.Parse(raw);
        var findings = await Rule.EvaluateAsync(diff, null);

        Assert.DoesNotContain(findings, f => f.Summary.Contains("parameter(s) added"));
    }

    [Fact]
    public async Task PublicMethodInTestFile_ShouldNotFlag()
    {
        // Test file helpers don't need null guards
        var raw = """
            diff --git a/src/ServiceTests.cs b/src/ServiceTests.cs
            index abc..def 100644
            --- a/src/ServiceTests.cs
            +++ b/src/ServiceTests.cs
            @@ -1,1 +1,3 @@
             // existing
            +private static Finding MakeFinding(string ruleId = "GCI0001")
            +{
            """;

        var diff = DiffParser.Parse(raw);
        var findings = await Rule.EvaluateAsync(diff, null);

        Assert.DoesNotContain(findings, f => f.Summary.Contains("parameter(s) added"));
    }

    [Fact]
    public async Task ExpressionBodyMethodNullableReturnNoParams_ShouldNotFlag()
    {
        // public override string? ToString() => (string?)Channel;
        // string? in return type and cast in body must not trigger parameter validation
        var raw = """
            diff --git a/src/Queue.cs b/src/Queue.cs
            index abc..def 100644
            --- a/src/Queue.cs
            +++ b/src/Queue.cs
            @@ -1,1 +1,2 @@
             // existing
            +public override string? ToString() => (string?)Channel;
            """;

        var diff = DiffParser.Parse(raw);
        var findings = await Rule.EvaluateAsync(diff, null);

        Assert.DoesNotContain(findings, f => f.Summary.Contains("parameter(s) added"));
    }

    [Fact]
    public async Task OverrideMethodWithNullableParam_ShouldNotFlag()
    {
        // Override methods cannot change the parameter contract declared by base/interface
        var raw = """
            diff --git a/src/Converter.cs b/src/Converter.cs
            index abc..def 100644
            --- a/src/Converter.cs
            +++ b/src/Converter.cs
            @@ -1,1 +1,3 @@
             // existing
            +public override void WriteJson(JsonWriter writer, object? value, JsonSerializer serializer)
            +{
            """;

        var diff = DiffParser.Parse(raw);
        var findings = await Rule.EvaluateAsync(diff, null);

        Assert.DoesNotContain(findings, f => f.Summary.Contains("parameter(s) added"));
    }

    [Fact]
    public async Task AbstractMethodWithNullableParam_ShouldNotFlag()
    {
        // Abstract methods have no body -- null validation cannot be added
        var raw = """
            diff --git a/src/Binder.cs b/src/Binder.cs
            index abc..def 100644
            --- a/src/Binder.cs
            +++ b/src/Binder.cs
            @@ -1,1 +1,2 @@
             // existing
            +public abstract Type BindToType(string? assemblyName, string typeName);
            """;

        var diff = DiffParser.Parse(raw);
        var findings = await Rule.EvaluateAsync(diff, null);

        Assert.DoesNotContain(findings, f => f.Summary.Contains("parameter(s) added"));
    }

    [Fact]
    public async Task DelegateDeclarationWithNullableParam_ShouldNotFlag()
    {
        // Delegate declarations have no body -- null validation cannot be added
        var raw = """
            diff --git a/src/Delegates.cs b/src/Delegates.cs
            index abc..def 100644
            --- a/src/Delegates.cs
            +++ b/src/Delegates.cs
            @@ -1,1 +1,2 @@
             // existing
            +public delegate void ExtensionDataSetter(object o, string key, object? value);
            """;

        var diff = DiffParser.Parse(raw);
        var findings = await Rule.EvaluateAsync(diff, null);

        Assert.DoesNotContain(findings, f => f.Summary.Contains("parameter(s) added"));
    }

    [Fact]
    public async Task ExpressionBodyPropertyOverride_ShouldNotFlagNullDereference()
    {
        // Expression-bodied property override is a declaration -- .Value IS the body
        var raw = """
            diff --git a/src/Reader.cs b/src/Reader.cs
            index abc..def 100644
            --- a/src/Reader.cs
            +++ b/src/Reader.cs
            @@ -1,1 +1,2 @@
             // existing
            +public override object? Value => _innerReader.Value;
            """;

        var diff = DiffParser.Parse(raw);
        var findings = await Rule.EvaluateAsync(diff, null);

        Assert.DoesNotContain(findings, f => f.Summary.Contains("null dereference"));
    }

    [Fact]
    public async Task NullForgivingValueAccess_ShouldNotFlag()
    {
        // .Value! -- developer has already asserted non-null with the null-forgiving operator
        var raw = """
            diff --git a/src/Converter.cs b/src/Converter.cs
            index abc..def 100644
            --- a/src/Converter.cs
            +++ b/src/Converter.cs
            @@ -1,1 +1,2 @@
             // existing
            +var s = (string)reader.Value!;
            """;

        var diff = DiffParser.Parse(raw);
        var findings = await Rule.EvaluateAsync(diff, null);

        Assert.DoesNotContain(findings, f => f.Summary.Contains("null dereference"));
    }

    [Fact]
    public async Task NullConditionalAfterValueAccess_ShouldNotFlag()
    {
        // .Value?.ToString() -- null-conditional after means developer is handling null safely
        var raw = """
            diff --git a/src/Converter.cs b/src/Converter.cs
            index abc..def 100644
            --- a/src/Converter.cs
            +++ b/src/Converter.cs
            @@ -1,1 +1,2 @@
             // existing
            +var s = reader.Value?.ToString();
            """;

        var diff = DiffParser.Parse(raw);
        var findings = await Rule.EvaluateAsync(diff, null);

        Assert.DoesNotContain(findings, f => f.Summary.Contains("null dereference"));
    }

    [Fact]
    public async Task NullConditionalBeforeValueAccess_ShouldNotFlag()
    {
        // ?.Value -- null-conditional before .Value guards the whole access
        var raw = """
            diff --git a/src/Converter.cs b/src/Converter.cs
            index abc..def 100644
            --- a/src/Converter.cs
            +++ b/src/Converter.cs
            @@ -1,1 +1,2 @@
             // existing
            +var s = reader?.Value;
            """;

        var diff = DiffParser.Parse(raw);
        var findings = await Rule.EvaluateAsync(diff, null);

        Assert.DoesNotContain(findings, f => f.Summary.Contains("null dereference"));
    }

    [Fact]
    public async Task ValuesPropertyAccess_ShouldNotFlag()
    {
        // .Values is a different property (e.g. Dictionary.Values) -- not a Nullable<T>.Value accessor
        var raw = """
            diff --git a/src/Store.cs b/src/Store.cs
            index abc..def 100644
            --- a/src/Store.cs
            +++ b/src/Store.cs
            @@ -1,1 +1,2 @@
             // existing
            +return _dictionary!.Values;
            """;

        var diff = DiffParser.Parse(raw);
        var findings = await Rule.EvaluateAsync(diff, null);

        Assert.DoesNotContain(findings, f => f.Summary.Contains("null dereference"));
    }

    [Fact]
    public async Task PublicMethodWithNullCoalescingParam_ShouldNotFlag()
    {
        // Method parameters with default values (including null-coalescing) don't need validation
        var raw = """
            diff --git a/src/Parser.cs b/src/Parser.cs
            index abc..def 100644
            --- a/src/Parser.cs
            +++ b/src/Parser.cs
            @@ -1,1 +1,3 @@
             // existing
            +public void Parse(string? input = "default")
            +{
            """;

        var diff = DiffParser.Parse(raw);
        var findings = await Rule.EvaluateAsync(diff, null);

        Assert.DoesNotContain(findings, f => f.Summary.Contains("parameter(s) added"));
    }

    [Fact]
    public async Task PublicMethodWithNullCoalescingInBody_ShouldNotFlag()
    {
        // Null-coalescing operator (??) in method body is a valid null-handling pattern
        var raw = """
            diff --git a/src/Processor.cs b/src/Processor.cs
            index abc..def 100644
            --- a/src/Processor.cs
            +++ b/src/Processor.cs
            @@ -1,1 +1,3 @@
             // existing
            +public void Process(string? input)
            +    var value = input ?? "default";
            """;

        var diff = DiffParser.Parse(raw);
        var findings = await Rule.EvaluateAsync(diff, null);

        Assert.DoesNotContain(findings, f => f.Summary.Contains("parameter(s) added"));
    }

    [Fact]
    public async Task PublicMethodWithNullCoalescingAssignmentInBody_ShouldNotFlag()
    {
        // Null-coalescing assignment (??=) in method body is a valid null-handling pattern
        var raw = """
            diff --git a/src/Cache.cs b/src/Cache.cs
            index abc..def 100644
            --- a/src/Cache.cs
            +++ b/src/Cache.cs
            @@ -1,1 +1,4 @@
             // existing
            +public void SetDefault(string? key)
            +{
            +    _cache[key] ??= GetDefault();
            """;

        var diff = DiffParser.Parse(raw);
        var findings = await Rule.EvaluateAsync(diff, null);

        Assert.DoesNotContain(findings, f => f.Summary.Contains("parameter(s) added"));
    }

    [Fact]
    public async Task PublicMethodWithoutNullHandling_ShouldStillFlag()
    {
        // Without any null-coalescing or default value, should flag
        var raw = """
            diff --git a/src/Service.cs b/src/Service.cs
            index abc..def 100644
            --- a/src/Service.cs
            +++ b/src/Service.cs
            @@ -1,1 +1,3 @@
             // existing
            +public void Execute(string? command)
            +{
            """;

        var diff = DiffParser.Parse(raw);
        var findings = await Rule.EvaluateAsync(diff, null);

        Assert.Contains(findings, f => f.Summary.Contains("parameter(s) added"));
    }

    [Fact]
    public async Task NRTEnabledFileWithNonNullableParam_ShouldNotFlag()
    {
        // In NRT-enabled files, 'string' (not 'string?') is non-nullable, so no validation needed
        var raw = """
            diff --git a/src/Modern.cs b/src/Modern.cs
            index abc..def 100644
            --- a/src/Modern.cs
            +++ b/src/Modern.cs
            @@ -1,1 +1,5 @@
             #nullable enable
            +public void ProcessData(string input)
            +{
            +    Console.WriteLine(input);
            +}
            """;

        var diff = DiffParser.Parse(raw);
        var findings = await Rule.EvaluateAsync(diff, null);

        Assert.DoesNotContain(findings, f => f.Summary.Contains("parameter(s) added"));
    }

    [Fact]
    public async Task PublicMethodWithNullCoalescingAssignment_ShouldNotFlag()
    {
        // Null-coalescing assignment (??=) in method body is a valid null-handling pattern
        var raw = """
            diff --git a/src/Processor.cs b/src/Processor.cs
            index abc..def 100644
            --- a/src/Processor.cs
            +++ b/src/Processor.cs
            @@ -1,1 +1,3 @@
             // existing
            +var cache = null;
             cache ??= new Cache();
            """;

        var diff = DiffParser.Parse(raw);
        var findings = await Rule.EvaluateAsync(diff, null);

        Assert.DoesNotContain(findings, f => f.Summary.Contains("null"));
    }

    [Fact]
    public async Task InitAccessorSuggestion_IndicatesModernCSharp()
    {
        // Properties with init accessor suggest NRT-enabled project
        var raw = """
            diff --git a/src/Settings.cs b/src/Settings.cs
            index abc..def 100644
            --- a/src/Settings.cs
            +++ b/src/Settings.cs
            @@ -1,1 +1,3 @@
            +public string Name { get; init; }
            +public void ApplySetting(string key)
            +{
            """;

        var diff = DiffParser.Parse(raw);
        var findings = await Rule.EvaluateAsync(diff, null);

        // init accessors indicate modern NRT-enabled code
        Assert.DoesNotContain(findings, f => f.Summary.Contains("parameter(s) added"));
    }
}
