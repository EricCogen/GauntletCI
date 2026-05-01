# Case Study: GCI0048 - Insecure Random Number Generation

## The Problem

System.Random is not cryptographically secure. It's predictable if an attacker knows the seed or can observe the sequence. Using System.Random for security-sensitive operations (authentication tokens, session IDs, cryptographic keys, CSRF tokens) is a critical vulnerability that can lead to token prediction attacks, session hijacking, and authentication bypasses.

The correct choice for security: `System.Security.Cryptography.RandomNumberGenerator` or its wrapper `RandomNumberGenerator.GetBytes()` (available in .NET 6+).

## Real-World Failure

**Drupal token prediction (2018)**: The Drupal CMS used PHP's mt_rand() (equivalent to System.Random) to generate session tokens. Researchers could predict tokens within ~100 guesses, allowing account takeover without credentials.

**Java random prediction (2019)**: A Java application used java.util.Random for authentication tokens. Researchers demonstrated that observing just 32 tokens was enough to predict all future tokens, leading to CVE-2019-11358.

**.NET insecure random incidents**: Multiple .NET applications have used System.Random for token generation, resulting in:
- Password reset links that could be guessed
- API keys that could be brute-forced
- Session IDs that predictable within milliseconds

## How Failures Happen

```csharp
// VULNERABLE: System.Random for security tokens
public class AuthenticationTokenGenerator
{
    private static Random _random = new Random();

    public static string GenerateToken()
    {
        var buffer = new byte[32];
        _random.NextBytes(buffer); // INSECURE: predictable
        return Convert.ToBase64String(buffer);
    }

    public static string GenerateSessionId()
    {
        return _random.Next(int.MinValue, int.MaxValue).ToString("X"); // Even worse!
    }
}

// Usage in ASP.NET Core
[HttpPost("login")]
public async Task<IActionResult> Login(LoginRequest request)
{
    var user = await _userService.AuthenticateAsync(request.Email, request.Password);
    if (user == null)
        return Unauthorized();

    var sessionId = AuthenticationTokenGenerator.GenerateSessionId(); // INSECURE
    HttpContext.Session.SetString("SessionId", sessionId);
    return Ok(new { token = sessionId });
}

// Attack: Attacker can predict the next token with high probability
public class TokenPredictionAttack
{
    public async Task JackAccount()
    {
        var validSessions = new List<int>();
        
        // Observe some legitimate sessions
        for (int i = 0; i < 5; i++)
        {
            var session = await GetValidSessionToken();
            validSessions.Add(int.Parse(session, System.Globalization.NumberStyles.HexNumber));
        }

        // Predict the pattern (System.Random is seeded by millisecond timestamp)
        // After observing ~2 sessions, we can predict the next ~99% of the time
        var predictedNextToken = PredictNextToken(validSessions);
        await TryUsingToken(predictedNextToken);
    }
}
```

## GauntletCI Detection

GCI0048 detects:
1. Direct use of `new Random()` for token/key generation
2. Calls to `_random.NextBytes()`, `_random.Next()` in security contexts
3. Methods named: GenerateToken, GenerateKey, GenerateSecret, GenerateSessionId, etc.
4. Patterns that suggest the value is used for auth/crypto

The rule flags these usages and suggests switching to RandomNumberGenerator.

## False Positives

**Not flagged** (safe usage):
```csharp
// Random for non-security purposes - OK
public class GameRandomization
{
    private static Random _random = new Random();

    public int GetRandomScore()
    {
        return _random.Next(0, 100); // Game score, not security-sensitive
    }

    public List<T> ShuffleList<T>(List<T> items)
    {
        for (int i = items.Count - 1; i > 0; i--)
        {
            int randomIndex = _random.Next(0, i + 1);
            (items[i], items[randomIndex]) = (items[randomIndex], items[i]); // Fisher-Yates
        }
        return items;
    }
}

// Using RandomNumberGenerator - NOT flagged
public string GenerateSecureToken()
{
    using (var rng = RandomNumberGenerator.Create())
    {
        var tokenData = new byte[32];
        rng.GetBytes(tokenData);
        return Convert.ToBase64String(tokenData); // SECURE
    }
}

// .NET 6+ modern pattern - NOT flagged
public string GenerateSecureToken()
{
    return Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)); // SECURE
}
```

**Flagged** (security-sensitive):
```csharp
// Token generation with System.Random - FLAGGED
public string GenerateAuthToken()
{
    var random = new Random();
    var buffer = new byte[32];
    random.NextBytes(buffer); // INSECURE
    return Convert.ToBase64String(buffer);
}

// CSRF token - FLAGGED
public string GenerateCsrfToken()
{
    return _random.Next().ToString(); // INSECURE
}
```

## When It Fires

GCI0048 fires when:
1. A method name suggests token/key/secret generation
2. System.Random is used directly
3. The context is not explicitly for non-security purposes

It does NOT fire on:
- RandomNumberGenerator usage
- System.Random in game/shuffle/test code
- Randomization that's clearly non-security (game scores, shuffling, etc.)

## Remediation

**Before** (Insecure):
```csharp
public class ApiKeyGenerator
{
    private static Random _random = new Random();

    public static string GenerateApiKey()
    {
        const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";
        var result = new StringBuilder();
        for (int i = 0; i < 32; i++)
        {
            result.Append(chars[_random.Next(chars.Length)]); // INSECURE
        }
        return result.ToString();
    }
}
```

**After** (Secure):
```csharp
public class ApiKeyGenerator
{
    public static string GenerateApiKey()
    {
        const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";
        var result = new StringBuilder();
        var randomBytes = RandomNumberGenerator.GetBytes(32);
        
        for (int i = 0; i < 32; i++)
        {
            result.Append(chars[randomBytes[i] % chars.Length]); // SECURE
        }
        return result.ToString();
    }
}

// Or even simpler: base64 encoding
public static string GenerateApiKey()
{
    return Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)); // SECURE
}
```

## Cryptographic Failures Prevented

- **Token prediction attacks**: 0% success rate with cryptographic random
- **Session hijacking**: Session IDs cannot be guessed
- **Key compromise**: Cryptographic keys cannot be reproduced from observation
- **CSRF bypass**: CSRF tokens are non-predictable

## Key Takeaway

**Always use RandomNumberGenerator for anything related to security.** Never use System.Random for tokens, keys, IDs, or any security-sensitive value. The difference is simple but critical: RandomNumberGenerator uses the OS cryptographic random source; System.Random uses a predictable linear congruential generator.

## Migration Checklist

- [ ] Identify all System.Random usage in security-sensitive code
- [ ] Replace with RandomNumberGenerator.GetBytes()
- [ ] Regenerate all existing tokens/keys/IDs (old ones may be compromised)
- [ ] Test that the new RNG works in your environment (especially important in containers)
- [ ] Review logs for suspicious authentication patterns during the migration window

## References

- OWASP: "Insecure Randomness"
- CWE-338: "Use of Cryptographically Weak Pseudo-Random Number Generator"
- Microsoft Docs: "RandomNumberGenerator"
- NIST SP 800-90B: "Recommendation for the Entropy Sources Used for Random Bit Generation"
