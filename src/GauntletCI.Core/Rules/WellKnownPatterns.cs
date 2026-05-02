// SPDX-License-Identifier: Elastic-2.0
namespace GauntletCI.Core.Rules;

internal static class WellKnownPatterns
{
    /// <summary>Variable and field name fragments used to detect hardcoded secrets by name (GCI0010, GCI0012).</summary>
    public static readonly string[] SecretNamePatterns = [ "password", "passwd", "secret", "apikey", "api_key", "token", "credential", "private_key", "privatekey", "access_key", "auth_key" ];

    /// <summary>Log-level keywords indicating high-severity log calls that warrant review (GCI0007, GCI0013).</summary>
    public static readonly string[] HighSeverityLogKeywords = [ "error", "exception", "critical", "fatal", "warn", "warning" ];

    /// <summary>
    /// Returns <c>true</c> when the given path belongs to a test or spec file.
    /// Used across rules to avoid false positives in test code.
    /// </summary>
    /// <param name="path">The file path to inspect.</param>
    public static bool IsTestFile(string path)
    {
        var normPath = path.Replace('\\', '/');
        var lastSlash = normPath.LastIndexOf('/');

        // Directory segment checks (both original-case and lowercase variants).
        if (lastSlash > 0)
        {
            foreach (var segment in normPath[..lastSlash].Split('/'))
            {
                var lower = segment.ToLowerInvariant();
                // Exact match for spec/specs directories (covers RSpec, Jest, etc.)
                if (lower == "spec" || lower == "specs") return true;
                // Word-boundary "test(s)" check on lowercase segment (avoids "latest", "protest")
                if (IsTestSegment(lower)) return true;
                // Benchmark / sample / example directories are not consumer-facing APIs
                if (IsNonProductionSegment(lower)) return true;
                // Mock / Fake infrastructure directories
                if (lower == "mock" || lower == "mocks" || lower == "fake" || lower == "fakes") return true;
                // PascalCase compound directory names: "IntegrationTests", "UnitTest", etc.
                if (segment.EndsWith("Tests", StringComparison.Ordinal)
                    || segment.EndsWith("Test", StringComparison.Ordinal)) return true;
                // PascalCase benchmark directories: "MyProject.Benchmarks", "Perf.Benchmark"
                if (segment.EndsWith("Benchmark", StringComparison.Ordinal)
                    || segment.EndsWith("Benchmarks", StringComparison.Ordinal)) return true;
            }
        }

        // File name: use original casing to distinguish PascalCase "Tests"/"Test"/"Spec" suffix
        // from English words that embed "test" (e.g. "Contest.cs", "Latest.cs", "Protest.cs").
        var origFile  = lastSlash >= 0 ? normPath[(lastSlash + 1)..] : normPath;
        var origNoExt = origFile.Contains('.') ? origFile[..origFile.LastIndexOf('.')] : origFile;
        return origNoExt.StartsWith("test", StringComparison.OrdinalIgnoreCase)
            || origNoExt.EndsWith("Tests", StringComparison.Ordinal)
            || origNoExt.EndsWith("Test",  StringComparison.Ordinal)
            || origNoExt.EndsWith("Spec",  StringComparison.OrdinalIgnoreCase)
            || origNoExt.EndsWith("Benchmark",  StringComparison.OrdinalIgnoreCase)
            || origNoExt.EndsWith("Benchmarks", StringComparison.OrdinalIgnoreCase);
    }

    // Returns true when a lowercase directory segment represents a test directory.
    // Requires "test" to appear at a word boundary: avoids "latest", "protest", etc.
    private static bool IsTestSegment(string segment)
    {
        if (segment.StartsWith("test")) return true;
        // EndsWith "test": only when the character immediately before "test" is non-letter
        // e.g. ".test", "-test", "_test" → yes; "latest" → 'a' precedes "test" → no
        if (segment.Length > 4 && segment.EndsWith("test") && !char.IsLetter(segment[^5])) return true;
        if (segment.Length > 5 && segment.EndsWith("tests") && !char.IsLetter(segment[^6])) return true;
        return false;
    }

    // Returns true when a directory segment represents a benchmark, sample, or example directory
    // that is not consumer-facing and should be treated like test code for rule suppression.
    private static bool IsNonProductionSegment(string segment)
    {
        // Benchmark projects: BenchmarkDotNet, microbenchmarks, perf projects
        if (segment.EndsWith("benchmark") || segment.EndsWith("benchmarks")) return true;
        if (segment == "benchmark" || segment == "benchmarks") return true;
        // Sample and example projects: demonstration code with no API stability guarantee
        if (segment == "samples" || segment == "sample") return true;
        if (segment == "examples" || segment == "example") return true;
        return false;
    }

    /// <summary>
    /// Returns <c>true</c> when the given path is an auto-generated file that should not be
    /// subject to rule analysis (source generators, designer files, scaffolded API clients, etc.).
    /// </summary>
    /// <param name="path">The file path to inspect.</param>
    public static bool IsGeneratedFile(string path)
    {
        var normPath = path.Replace('\\', '/');

        // Directory segment: any path with a /Generated/ folder is auto-generated
        if (normPath.Contains("/Generated/", StringComparison.OrdinalIgnoreCase)) return true;
        // Build output or intermediate artifacts
        if (normPath.Contains("/obj/", StringComparison.OrdinalIgnoreCase)) return true;

        var fileName = normPath.Contains('/')
            ? normPath[(normPath.LastIndexOf('/') + 1)..]
            : normPath;

        // Roslyn source generator outputs: Foo.g.cs, Foo.g.i.cs
        if (fileName.EndsWith(".g.cs", StringComparison.OrdinalIgnoreCase)) return true;
        if (fileName.EndsWith(".g.i.cs", StringComparison.OrdinalIgnoreCase)) return true;
        // WinForms / WPF designer files
        if (fileName.EndsWith(".Designer.cs", StringComparison.OrdinalIgnoreCase)) return true;
        // Assembly-level attribute file emitted by SDK
        if (fileName.Equals("AssemblyInfo.cs", StringComparison.OrdinalIgnoreCase)) return true;
        // API surface manifest files emitted by the .NET SDK:
        //   net8.0.cs, net10.0.cs (numeric TFMs) and netstandard2.0.cs, netstandard2.1.cs, etc.
        // These enumerate every public member and are never hand-authored.
        if (System.Text.RegularExpressions.Regex.IsMatch(
                fileName, @"\.(net\d+\.\d+|netstandard\d+\.\d+)\.cs$",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase)) return true;

        return false;
    }

    /// <summary>
    /// Returns <c>true</c> when <paramref name="addedSig"/> is a backward-compatible extension of
    /// <paramref name="removedSig"/> (i.e. the added overload appends only optional parameters,
    /// or the only difference is the addition of modifier keywords like <c>virtual</c> or <c>override</c>).
    /// Used by GCI0003 and GCI0004.
    /// </summary>
    public static bool IsBackwardCompatibleExtension(string removedSig, string addedSig)
    {
        // Adding/removing modifier keywords (virtual, override, sealed, abstract, new) does not
        // break existing callers at the binary level.
        if (NormalizeModifiers(removedSig) == NormalizeModifiers(addedSig)) return true;

        var removedParams = ExtractParenContent(removedSig)?.Trim() ?? "";
        var addedParams   = ExtractParenContent(addedSig)?.Trim()   ?? "";

        if (addedParams.Length <= removedParams.Length) return false;
        if (!addedParams.StartsWith(removedParams, StringComparison.Ordinal)) return false;

        var extra = addedParams[removedParams.Length..].TrimStart(',').TrimStart();
        return !string.IsNullOrWhiteSpace(extra) && extra.Contains('=', StringComparison.Ordinal);
    }

    /// <summary>
    /// Strips common C# modifier keywords so signatures that differ only in virtual/override/sealed/abstract/new
    /// can be compared for semantic equivalence.
    /// </summary>
    private static string NormalizeModifiers(string sig)
    {
        static string Strip(string s, string keyword)
            => s.Replace(keyword, " ", StringComparison.Ordinal);

        var s = sig.Trim();
        s = Strip(s, "virtual ");
        s = Strip(s, "override ");
        s = Strip(s, "sealed ");
        s = Strip(s, "abstract ");
        s = Strip(s, " new ");
        // Collapse multiple spaces introduced by stripping
        while (s.Contains("  ", StringComparison.Ordinal))
            s = s.Replace("  ", " ", StringComparison.Ordinal);
        return s.Trim();
    }

    /// <summary>Extracts the parameter list content between the outermost parentheses of a method signature.</summary>
    public static string? ExtractParenContent(string sig)
    {
        var open  = sig.IndexOf('(');
        var close = sig.LastIndexOf(')');
        return open >= 0 && close > open ? sig[(open + 1)..close] : null;
    }

    /// <summary>
    /// NRT (Nullable Reference Type) guards for detecting nullability-related patterns.
    /// Used by GCI0006, GCI0043, and other nullability-aware rules to reduce false positives.
    /// </summary>

    /// <summary>
    /// Returns <c>true</c> when NRT (Nullable Reference Type) is enabled for the given file.
    /// NRT is enabled via: #nullable enable directive, project-wide settings, or modern .NET versions.
    /// Used by GCI0006 and GCI0043 to determine if 'string' parameters are non-nullable by default.
    /// </summary>
    public static bool IsNullableReferenceTypeEnabled(string fileContent)
    {
        // Explicit NRT directive: #nullable enable or #nullable restore
        if (fileContent.Contains("#nullable enable", StringComparison.OrdinalIgnoreCase) ||
            fileContent.Contains("#nullable restore", StringComparison.OrdinalIgnoreCase))
            return true;

        // Explicit NRT disable: #nullable disable indicates NRT is not active
        if (fileContent.Contains("#nullable disable", StringComparison.OrdinalIgnoreCase))
            return false;

        // Heuristic: Modern .NET projects (net5+) typically have NRT enabled
        // Look for patterns that indicate modern C# (nullable annotations, record types, init accessors)
        if (fileContent.Contains(" record ", StringComparison.Ordinal) ||
            fileContent.Contains("{ init; }", StringComparison.Ordinal) ||
            fileContent.Contains("{ get; init; }", StringComparison.Ordinal))
            return true;

        // Look for the pattern: non-nullable string used in method signatures
        // This is stronger evidence of NRT enablement than just presence of 'string'
        // Pattern: public/protected method with 'string' param not followed by '?'
        if (System.Text.RegularExpressions.Regex.IsMatch(
                fileContent, @"(public|protected)\s+\w+\s+\w+\s*\(\s*string\s+\w+"))
            return true;

        // Default: assume NRT disabled (conservative approach - will validate parameters)
        return false;
    }

    /// <summary>
    /// Returns <c>true</c> when the parameter section contains explicitly non-nullable parameters
    /// (e.g., 'string param' without '?'). In NRT-enabled context, these don't need validation.
    /// </summary>
    public static bool HasNonNullableParams(string paramSection)
    {
        // Look for 'string' not followed by '?' (indicating non-nullable in NRT context)
        int angleDepth = 0;
        for (int i = 0; i < paramSection.Length; i++)
        {
            char c = paramSection[i];
            if (c == '<') { angleDepth++; continue; }
            if (c == '>') { angleDepth = Math.Max(0, angleDepth - 1); continue; }
            if (angleDepth > 0) continue;

            // Match "string" not followed by "?"
            if (i + 6 <= paramSection.Length && paramSection.AsSpan(i, 6).SequenceEqual("string"))
            {
                if (i + 6 >= paramSection.Length || paramSection[i + 6] != '?')
                {
                    // Check boundary
                    bool leadOk = i == 0 || paramSection[i - 1] is ' ' or '(' or ',' or '<';
                    bool trailOk = i + 6 >= paramSection.Length || paramSection[i + 6] is ' ' or '[' or ',' or ')';
                    if (leadOk && trailOk) return true;
                }
            }
        }
        return false;
    }

    /// <summary>
    /// Returns <c>true</c> when the parameter list contains nullable parameters (e.g., 'string?' or 'object?').
    /// Used by GCI0006 to detect when public methods have nullable reference type parameters.
    /// </summary>
    public static bool HasNullableReferenceParam(string paramSection)
    {
        // Walk character by character, tracking generic depth so we skip type arguments
        // like Dictionary<string?, int> and only match top-level parameters.
        int angleDepth = 0;
        for (int i = 0; i < paramSection.Length; i++)
        {
            char c = paramSection[i];
            if (c == '<') { angleDepth++; continue; }
            if (c == '>') { angleDepth = Math.Max(0, angleDepth - 1); continue; }
            if (angleDepth > 0) continue;

            foreach (var keyword in new[] { "string?", "object?" })
            {
                if (i + keyword.Length > paramSection.Length) continue;
                if (!paramSection.AsSpan(i).StartsWith(keyword, StringComparison.Ordinal)) continue;

                // Leading boundary: must be preceded by a non-identifier char
                bool leadOk = i == 0 || paramSection[i - 1] is ' ' or '(' or ',' or '<';
                if (!leadOk) continue;

                // Trailing boundary: must be followed by a non-identifier char
                int after = i + keyword.Length;
                bool trailOk = after >= paramSection.Length ||
                               paramSection[after] is ' ' or '[' or ',' or ')' or '<';
                if (trailOk) return true;
            }
        }
        return false;
    }

    /// <summary>
    /// Returns <c>true</c> when the content contains Nullable&lt;T&gt; where T is a value type.
    /// In NRT context, Nullable&lt;int&gt;, Nullable&lt;string&gt;, etc. always have a value.
    /// </summary>
    public static bool IsNullableOfNonNullableType(string content)
    {
        // Look for Nullable<T> or Nullable<...> patterns
        var match = System.Text.RegularExpressions.Regex.Match(content, @"Nullable<(\w+(?:<[^>]+>)?)>");
        if (!match.Success) return false;

        var typeParam = match.Groups[1].Value;
        
        // If T is a value type (int, bool, DateTime, etc.), Nullable<T> always has a value in NRT context
        var valueTypes = new[] 
        { 
            "int", "long", "short", "byte", "double", "float", "decimal", "bool", 
            "uint", "ulong", "ushort", "ubyte", "char",
            "DateTime", "TimeSpan", "DateOnly", "TimeOnly", "Guid",
            "DateTimeOffset", "DateTimeKind"
        };

        // Also check for custom structs (heuristic: if it's PascalCase and not a built-in type)
        bool isValueType = valueTypes.Contains(typeParam);
        bool isCustomStruct = typeParam.Length > 0 && char.IsUpper(typeParam[0]) && !valueTypes.Contains(typeParam);

        return isValueType || isCustomStruct;
    }

    /// <summary>
    /// Returns <c>true</c> when the content contains a #pragma warning disable with nullable-related codes.
    /// Detects suppression of nullable reference type warnings (CS8600, CS8603, etc.).
    /// Used by GCI0043 to flag deliberate nullable warning suppression.
    /// </summary>
    public static bool IsPragmaNullableDisable(string content)
    {
        if (!content.Contains("#pragma warning disable", StringComparison.OrdinalIgnoreCase))
            return false;
        
        var nullableCodes = new[] { "nullable", "CS8600", "CS8601", "CS8602", "CS8603", "CS8604" };
        return nullableCodes.Any(code =>
            content.Contains(code, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Returns <c>true</c> when the content contains a LINQ expression where .Value is intentionally mapped.
    /// Patterns: .Select(x => x.Value), .Where(x => x.Value != null), etc.
    /// Used by GCI0006 to avoid flagging safe LINQ projections as unsafe dereferences.
    /// </summary>
    public static bool IsLinqValueProjection(string content)
    {
        var linqMethods = new[] { "Select", "SelectMany", "Where", "OrderBy", "OrderByDescending", 
                                  "GroupBy", "All", "Any", "First", "FirstOrDefault", 
                                  "Last", "LastOrDefault", "Single", "SingleOrDefault" };

        foreach (var method in linqMethods)
        {
            // Pattern: .MethodName(... => ....Value...)
            var pattern = @"\." + method + @"\s*\([^)]*=>.*\.Value";
            if (System.Text.RegularExpressions.Regex.IsMatch(content, pattern, System.Text.RegularExpressions.RegexOptions.IgnoreCase))
                return true;
        }

        return false;
    }

    /// <summary>
    /// HTTP Client and external service framework-specific guards.
    /// Used by GCI0039 and related rules to reduce false positives on framework-specific timeout patterns.
    /// </summary>

    /// <summary>
    /// Returns <c>true</c> when the file path indicates gRPC-related code.
    /// gRPC channels manage timeouts at the channel/connection level, not per-HttpClient.
    /// Used by GCI0039 to skip false positive timeout checks in gRPC contexts.
    /// </summary>
    public static bool IsGrpcRelatedFile(string path)
    {
        return path.Contains("grpc", StringComparison.OrdinalIgnoreCase)
            || path.Contains("channel", StringComparison.OrdinalIgnoreCase)
            || path.EndsWith(".g.cs", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Returns <c>true</c> when the added lines indicate HttpClient configuration via IHttpClientFactory.
    /// Factory-managed clients configure timeout at the handler/channel level, not per-client.
    /// </summary>
    public static bool IsHttpFactoryConfigured(System.Collections.Generic.List<Diff.DiffLine> addedLines)
    {
        return addedLines.Any(l =>
            l.Content.Contains("IHttpClientFactory", StringComparison.Ordinal)
            || l.Content.Contains("AddHttpClient", StringComparison.Ordinal)
            || l.Content.Contains("HttpClientFactoryOptions", StringComparison.Ordinal));
    }

    /// <summary>
    /// Returns <c>true</c> when the added lines indicate gRPC channel configuration.
    /// gRPC channels manage timeouts via GrpcChannelOptions at the connection level.
    /// </summary>
    public static bool UsesGrpcChannel(System.Collections.Generic.List<Diff.DiffLine> addedLines)
    {
        return addedLines.Any(l =>
            l.Content.Contains("GrpcChannel", StringComparison.Ordinal)
            || l.Content.Contains("ChannelOptions", StringComparison.Ordinal)
            || l.Content.Contains("GrpcChannelOptions", StringComparison.Ordinal))
            || addedLines.Any(l => 
                l.Content.Contains("HttpClientHandler", StringComparison.Ordinal)
                && addedLines.Any(hl => hl.Content.Contains("GrpcChannel", StringComparison.Ordinal)));
    }

    /// <summary>
    /// Returns <c>true</c> when the added lines indicate use of factory-managed or injected HTTP clients.
    /// Factory patterns, Polly policies, and DI-managed clients manage timeouts externally.
    /// </summary>
    public static bool UsesFactoryManagedHttpClients(System.Collections.Generic.List<Diff.DiffLine> addedLines)
    {
        var factoryPatterns = new[]
        {
            "IHttpClientFactory", "AddHttpClient", "HttpClientFactoryOptions",
            "AddPolicyHandler", "AddTransientHttpErrorPolicy", "Polly"
        };

        return addedLines.Any(l =>
            factoryPatterns.Any(p => l.Content.Contains(p, StringComparison.Ordinal)));
    }

    /// <summary>
    /// Returns <c>true</c> when the content indicates an injected or static HttpClient is being used.
    /// Patterns like _httpClient.GetAsync() or this.client.PostAsync() are typically DI-managed.
    /// </summary>
    public static bool IsInjectedOrStaticClient(string content)
    {
        var injectionPatterns = new[]
        {
            "_httpClient", "_client", "this.client", "this._client",
            "httpClient.", "_http.", "HttpClient."
        };

        var httpMethods = new[] { ".GetAsync(", ".PostAsync(", ".PutAsync(", ".SendAsync(" };

        return injectionPatterns.Any(p =>
            content.Contains(p, StringComparison.Ordinal) &&
            httpMethods.Any(m => content.Contains(m)));
    }

    /// <summary>
    /// Dependency Injection framework-specific guards.
    /// Used by GCI0038 and related rules to reduce false positives in DI infrastructure files and test contexts.
    /// </summary>

    /// <summary>
    /// Returns <c>true</c> when the file path indicates infrastructure/configuration code where DI setup occurs.
    /// Service locator patterns and direct instantiation are acceptable in Program.cs, Startup.cs, etc.
    /// </summary>
    public static bool IsInfrastructureFile(string path)
    {
        var fileName = System.IO.Path.GetFileName(path);
        return string.Equals(fileName, "Program.cs", StringComparison.OrdinalIgnoreCase)
            || string.Equals(fileName, "Startup.cs", StringComparison.OrdinalIgnoreCase)
            || fileName.EndsWith("Extensions.cs", StringComparison.OrdinalIgnoreCase)
            || path.Contains("ServiceCollection", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Array of service locator patterns that violate DI principles.
    /// These patterns bypass DI and make testing/mocking difficult.
    /// </summary>
    public static readonly string[] ServiceLocatorPatterns =
    [
        "provider.GetService<",
        "provider.GetRequiredService<",
        "serviceProvider.GetService<",
        "serviceProvider.GetRequiredService<",
        "_serviceProvider.GetService<",
        "_serviceProvider.GetRequiredService<",
    ];

    /// <summary>
    /// Regex to detect direct instantiation of injectable types.
    /// Matches patterns like: new UserService(...), new OrderRepository(...), new RequestHandler(...)
    /// </summary>
    public static readonly System.Text.RegularExpressions.Regex DirectInstantiationRegex =
        new(@"new [A-Z][a-zA-Z]*(Service|Repository|Manager|Handler|Client)\s*\(", System.Text.RegularExpressions.RegexOptions.Compiled);

    /// <summary>
    /// Patterns to exclude from direct instantiation checks.
    /// Test doubles and event handlers are legitimate cases for direct instantiation.
    /// </summary>
    public static readonly string[] DirectInstantiationExclusions =
    [
        "//",  // comment
        "Mock<", "Fake<", "Stub<", "Spy<",  // test doubles
        "EventHandler(", "new EventHandler",  // event handlers
        "+= new",  // event subscription
        "var mock", "var fake", "var stub", "var spy",  // test variable patterns
        "CreateMock", "CreateFake", "CreateStub",  // test factory methods
    ];
}
