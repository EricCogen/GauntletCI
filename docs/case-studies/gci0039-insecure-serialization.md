# Case Study: GCI0039 - Insecure Deserialization

## The Problem

Deserializing untrusted data can lead to Remote Code Execution (RCE). If an attacker can control the serialized bytes/XML/JSON being deserialized, they can instantiate arbitrary types with arbitrary values. In .NET, dangerous deserializers include:
- BinaryFormatter (deprecated, dangerous by design)
- LosFormatter / ObjectStateFormatter (legacy ASP.NET)
- Unsafe XAML deserialization
- Some TypeScript/JavaScript serializers

Safe deserialization requires: using only safe types (JSON, explicitly typed DTOs), avoiding polymorphic deserialization, and using safe libraries.

## Real-World Failure

**ObjectInputStream gadget chains (Java 2014)**: Researchers demonstrated that deserializing untrusted Java objects could execute arbitrary code via "gadget chains"—unexpected sequences of existing library methods. ysoserial tool was released, making it trivial to craft RCE payloads. Years of Java apps were vulnerable.

**BinaryFormatter RCE (2020-2022)**: Microsoft deprecated BinaryFormatter due to inherent security risks. Thousands of .NET applications were vulnerable to RCE via BinaryFormatter. Attackers could send crafted binary data to ASP.NET Core APIs, and if the app used BinaryFormatter, arbitrary code would execute.

**Telerik Upload file RCE (2017)**: Telerik UI components had a deserialization vulnerability. Attackers uploaded crafted files that, when deserialized, executed code. Thousands of web applications were compromised. The fix was to remove deserialization entirely.

**.NET RemotingServices RCE**: Legacy .NET Remoting used BinaryFormatter by default. A single network call could trigger RCE.

## How Failures Happen

```csharp
// DANGEROUS: BinaryFormatter deserialization
public class FileProcessor
{
    public static object DeserializeUserData(byte[] data)
    {
        var formatter = new BinaryFormatter();
        using (var ms = new MemoryStream(data))
        {
            return formatter.Deserialize(ms); // RCE vulnerability!
        }
    }

    public void ProcessUploadedFile(byte[] fileData)
    {
        try
        {
            var userObject = DeserializeUserData(fileData); // Untrusted data!
            ProcessObject(userObject);
        }
        catch (SerializationException ex)
        {
            Console.WriteLine($"Failed to deserialize: {ex.Message}");
        }
    }
}

// Attack: Attacker crafts malicious binary payload
public class RceExploit
{
    public async Task ExploitVulnerability(string uploadUrl)
    {
        // ysoserial.net generates this malicious binary payload
        var gadgetChainPayload = GenerateMaliciousPayload(
            "System.Diagnostics.Process.Start('calc.exe')");

        var response = await _httpClient.PostAsync(
            uploadUrl,
            new ByteArrayContent(gadgetChainPayload));

        // File uploaded. Attacker's code executes on the server!
    }

    private byte[] GenerateMaliciousPayload(string command)
    {
        // Uses ObjectDataProvider gadget chain or similar
        // to instantiate Process and execute the command
        return _payloadGenerator.Generate(command);
    }
}

// Even JSON can be risky if deserializing to object
public class UnSafeJsonDeserialization
{
    public object ProcessJsonRequest(string json)
    {
        var settings = new JsonSerializerSettings
        {
            TypeNameHandling = TypeNameHandling.Auto // DANGEROUS!
        };
        return JsonConvert.DeserializeObject(json, settings);
    }

    // Request: {"$type":"System.Diagnostics.Process","StartInfo":{...}}
    // If TypeNameHandling.Auto, this creates a Process instance and starts it!
}
```

## GauntletCI Detection

GCI0039 detects:
1. `BinaryFormatter.Deserialize()`
2. `LosFormatter.Deserialize()`
3. `ObjectStateFormatter.Deserialize()`
4. `XamlReader.Parse()` on untrusted input
5. JSON deserialization with `TypeNameHandling.Auto` or `TypeNameHandling.Objects`
6. `JsonConvert.DeserializeObject(data, Type.GetType(...))`
7. Other dangerous type-aware deserializers

The rule assumes deserialization of untrusted data unless there's explicit evidence it's from a trusted source.

## False Positives

**Not flagged** (safe deserialization):
```csharp
// Safe: Deserializing to a specific, safe type
public class UserRequest
{
    public string Name { get; set; }
    public string Email { get; set; }
}

public UserRequest ProcessRequest(string json)
{
    return JsonConvert.DeserializeObject<UserRequest>(json); // SAFE - specific type
}

// Safe: Using System.Text.Json (no polymorphic deserialization)
public User ProcessRequest(string json)
{
    return JsonSerializer.Deserialize<User>(json); // SAFE by default
}

// Safe: Explicitly trusted source
public CachedData LoadFromTrustedCache()
{
    var cachedData = _cache.Get("key") as byte[];
    if (cachedData == null) return null;

    // Assuming cache is trusted (not user input), this is acceptable
    var formatter = new BinaryFormatter();
    return formatter.Deserialize(new MemoryStream(cachedData)); // Acceptable with comment
}
```

**Flagged** (dangerous deserialization):
```csharp
// Dangerous: BinaryFormatter on any stream - FLAGGED
var obj = new BinaryFormatter().Deserialize(stream);

// Dangerous: Type-aware JSON deserialization - FLAGGED
var obj = JsonConvert.DeserializeObject(json, 
    TypeNameHandling.Auto);

// Dangerous: Dynamic type deserialization - FLAGGED
Type userType = Type.GetType(request.TypeName);
var obj = JsonConvert.DeserializeObject(json, userType);
```

## When It Fires

GCI0039 fires when:
1. A known dangerous deserializer is called (BinaryFormatter, LosFormatter, etc.)
2. The data being deserialized is from an external source (request, file, network)
3. The deserialization is not to a specific, safe type

It does NOT fire on:
- JsonSerializer (System.Text.Json) with specific types
- Safe XML deserialization (XDocument.Parse is OK)
- BinaryFormatter on explicitly trusted data (with clear comments)

## Remediation

**Before** (RCE Vulnerability):
```csharp
[ApiController]
[Route("api/[controller]")]
public class DataController : ControllerBase
{
    [HttpPost("upload")]
    public IActionResult Upload(IFormFile file)
    {
        var data = new byte[file.Length];
        file.OpenReadStream().Read(data);

        // VULNERABLE: No type validation
        var formatter = new BinaryFormatter();
        var uploadedObject = formatter.Deserialize(new MemoryStream(data)); // RCE!
        
        _database.SaveObject(uploadedObject);
        return Ok();
    }
}
```

**After** (Safe):
```csharp
// Define safe DTOs
public class UserUploadDto
{
    [MaxLength(100)]
    public string Name { get; set; }
    [EmailAddress]
    public string Email { get; set; }
    [Range(0, 120)]
    public int Age { get; set; }
}

[ApiController]
[Route("api/[controller]")]
public class DataController : ControllerBase
{
    [HttpPost("upload")]
    public IActionResult Upload([FromBody] UserUploadDto uploadData)
    {
        // Model binding automatically deserializes to the safe DTO type
        // No RCE possible because we only deserialize to UserUploadDto
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var user = _mapper.Map<User>(uploadData);
        _database.SaveUser(user);
        return Ok();
    }

    // JSON alternative (if using raw JSON)
    [HttpPost("json-upload")]
    public IActionResult JsonUpload(string jsonData)
    {
        try
        {
            // Safe: Deserialize to specific type only
            var uploadData = JsonSerializer.Deserialize<UserUploadDto>(jsonData);
            if (uploadData == null)
                return BadRequest("Invalid data");

            var user = _mapper.Map<User>(uploadData);
            _database.SaveUser(user);
            return Ok();
        }
        catch (JsonException ex)
        {
            return BadRequest($"Invalid JSON: {ex.Message}");
        }
    }
}
```

## Safe Deserialization Rules

1. **Never deserialize to `object` or abstract types**—always specify the concrete target type
2. **Avoid BinaryFormatter entirely**—use JSON or XML with type restrictions
3. **Disable TypeNameHandling** in Newtonsoft.Json (use `TypeNameHandling.None`)
4. **Use System.Text.Json** (modern, safe by default)
5. **Whitelist types** if polymorphic deserialization is unavoidable
6. **Validate after deserialization**—check that the object matches expected structure

## Attack Prevention Checklist

- [ ] Audit all deserialization code for dangerous patterns
- [ ] Replace BinaryFormatter with JSON or XML
- [ ] Ensure JSON deserialization targets specific safe types
- [ ] Remove TypeNameHandling.Auto from anywhere it appears
- [ ] Add input validation after deserialization
- [ ] Test with ysoserial.net payloads to confirm vulnerability is fixed

## References

- Microsoft: "BinaryFormatter security guide"
- OWASP: "Deserialization of Untrusted Data"
- CWE-502: "Deserialization of Untrusted Data"
- ysoserial.net: Gadget chain generator (testing tool)
- Frohoff & Lawrence: "Exploiting Deserialization Vulnerabilities" (DefCon 23)
