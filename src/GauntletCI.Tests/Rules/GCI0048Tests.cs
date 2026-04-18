// SPDX-License-Identifier: Elastic-2.0
using GauntletCI.Core.Diff;
using GauntletCI.Core.Rules.Implementations;

namespace GauntletCI.Tests.Rules;

public class GCI0048Tests
{
    private static readonly GCI0048_InsecureRandomInSecurityContext Rule = new();

    [Fact]
    public async Task NewRandomNearToken_ShouldFire()
    {
        var raw = """
            diff --git a/src/Auth/TokenService.cs b/src/Auth/TokenService.cs
            index abc..def 100644
            --- a/src/Auth/TokenService.cs
            +++ b/src/Auth/TokenService.cs
            @@ -1,5 +1,8 @@
             public class TokenService {
            +    public string GenerateToken() {
            +        var random = new Random();
            +        var token = random.Next(100000, 999999).ToString();
            +        return token;
            +    }
             }
            """;

        var diff = DiffParser.Parse(raw);
        var findings = await Rule.EvaluateAsync(diff, null);

        Assert.NotEmpty(findings);
        Assert.Contains(findings, f => f.RuleId == "GCI0048");
    }

    [Fact]
    public async Task NewRandomNearPassword_ShouldFire()
    {
        var raw = """
            diff --git a/src/Security/PasswordGen.cs b/src/Security/PasswordGen.cs
            index abc..def 100644
            --- a/src/Security/PasswordGen.cs
            +++ b/src/Security/PasswordGen.cs
            @@ -1,4 +1,6 @@
             public class PasswordGen {
            +    private static readonly Random _rng = new Random();
            +    public string GeneratePassword(int length) =>
            +        new string(Enumerable.Range(0, length).Select(_ => _alphabet[_rng.Next(_alphabet.Length)]).ToArray());
             }
            """;

        var diff = DiffParser.Parse(raw);
        var findings = await Rule.EvaluateAsync(diff, null);

        Assert.NotEmpty(findings);
    }

    [Fact]
    public async Task NewRandomFarFromSecurityContext_ShouldNotFire()
    {
        var raw = """
            diff --git a/src/Utils/Shuffler.cs b/src/Utils/Shuffler.cs
            index abc..def 100644
            --- a/src/Utils/Shuffler.cs
            +++ b/src/Utils/Shuffler.cs
            @@ -1,4 +1,7 @@
             public class Shuffler {
            +    public List<T> Shuffle<T>(List<T> items) {
            +        var rng = new Random();
            +        return items.OrderBy(_ => rng.Next()).ToList();
            +    }
             }
            """;

        var diff = DiffParser.Parse(raw);
        var findings = await Rule.EvaluateAsync(diff, null);

        Assert.Empty(findings);
    }

    [Fact]
    public async Task NewRandomInTestFile_ShouldNotFire()
    {
        var raw = """
            diff --git a/src/Tests/TokenServiceTests.cs b/src/Tests/TokenServiceTests.cs
            index abc..def 100644
            --- a/src/Tests/TokenServiceTests.cs
            +++ b/src/Tests/TokenServiceTests.cs
            @@ -1,4 +1,6 @@
             public class TokenServiceTests {
            +    [Fact]
            +    public void Token_IsGenerated() {
            +        var rng = new Random();
            +        var secret = rng.Next(1000, 9999).ToString();
            +    }
             }
            """;

        var diff = DiffParser.Parse(raw);
        var findings = await Rule.EvaluateAsync(diff, null);

        Assert.Empty(findings);
    }

    [Fact]
    public async Task NewRandomNearSalt_ShouldFire()
    {
        var raw = """
            diff --git a/src/Crypto/Hasher.cs b/src/Crypto/Hasher.cs
            index abc..def 100644
            --- a/src/Crypto/Hasher.cs
            +++ b/src/Crypto/Hasher.cs
            @@ -1,5 +1,7 @@
             public class Hasher {
            +    public byte[] GenerateSalt() {
            +        var rng = new Random();
            +        var salt = new byte[16];
            +        rng.NextBytes(salt);
            +        return salt;
            +    }
             }
            """;

        var diff = DiffParser.Parse(raw);
        var findings = await Rule.EvaluateAsync(diff, null);

        Assert.NotEmpty(findings);
    }
}
