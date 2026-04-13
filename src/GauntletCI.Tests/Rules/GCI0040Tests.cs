// SPDX-License-Identifier: Elastic-2.0
using GauntletCI.Core.Diff;
using GauntletCI.Core.Model;
using GauntletCI.Core.Rules.Implementations;

namespace GauntletCI.Tests.Rules;

public class GCI0040Tests
{
    private static readonly GCI0040_AuthorizationCoverage Rule = new();

    [Fact]
    public async Task ControllerActionWithoutAuthorize_ShouldFlagMedium()
    {
        var raw = """
            diff --git a/src/UsersController.cs b/src/UsersController.cs
            index abc..def 100644
            --- a/src/UsersController.cs
            +++ b/src/UsersController.cs
            @@ -1,3 +1,6 @@
             public class UsersController : ControllerBase {
            +    public IActionResult GetAll()
            +    {
            +        return Ok();
            +    }
             }
            """;

        var diff = DiffParser.Parse(raw);
        var findings = await Rule.EvaluateAsync(diff, null);

        Assert.Contains(findings, f =>
            f.Summary.Contains("Controller action added without authorization attribute") &&
            f.Confidence == Confidence.Medium);
    }

    [Fact]
    public async Task ControllerActionWithAuthorize_ShouldNotFlagUnprotected()
    {
        var raw = """
            diff --git a/src/UsersController.cs b/src/UsersController.cs
            index abc..def 100644
            --- a/src/UsersController.cs
            +++ b/src/UsersController.cs
            @@ -1,3 +1,7 @@
             public class UsersController : ControllerBase {
            +    [Authorize]
            +    public IActionResult GetAll()
            +    {
            +        return Ok();
            +    }
             }
            """;

        var diff = DiffParser.Parse(raw);
        var findings = await Rule.EvaluateAsync(diff, null);

        Assert.DoesNotContain(findings, f =>
            f.Summary.Contains("Controller action added without authorization attribute"));
    }

    [Fact]
    public async Task ControllerActionWithAllowAnonymous_ShouldNotFlagUnprotected()
    {
        var raw = """
            diff --git a/src/AuthController.cs b/src/AuthController.cs
            index abc..def 100644
            --- a/src/AuthController.cs
            +++ b/src/AuthController.cs
            @@ -1,3 +1,7 @@
             public class AuthController : ControllerBase {
            +    [AllowAnonymous]
            +    public IActionResult Login()
            +    {
            +        return Ok();
            +    }
             }
            """;

        var diff = DiffParser.Parse(raw);
        var findings = await Rule.EvaluateAsync(diff, null);

        Assert.DoesNotContain(findings, f =>
            f.Summary.Contains("Controller action added without authorization attribute"));
    }

    [Fact]
    public async Task HardcodedRoleString_ShouldFlagMedium()
    {
        var raw = """
            diff --git a/src/AdminController.cs b/src/AdminController.cs
            index abc..def 100644
            --- a/src/AdminController.cs
            +++ b/src/AdminController.cs
            @@ -1,3 +1,4 @@
             public class AdminController : ControllerBase {
            +    [Authorize(Roles = "Admin")]
             }
            """;

        var diff = DiffParser.Parse(raw);
        var findings = await Rule.EvaluateAsync(diff, null);

        Assert.Contains(findings, f =>
            f.Summary.Contains("Hardcoded role string in authorization attribute") &&
            f.Confidence == Confidence.Medium);
    }

    [Fact]
    public async Task ValidateIssuerFalse_ShouldFlagHigh()
    {
        var raw = """
            diff --git a/src/Startup.cs b/src/Startup.cs
            index abc..def 100644
            --- a/src/Startup.cs
            +++ b/src/Startup.cs
            @@ -1,3 +1,5 @@
             services.AddAuthentication().AddJwtBearer(options => {
            +    options.TokenValidationParameters = new TokenValidationParameters {
            +        ValidateIssuer = false
            +    };
             });
            """;

        var diff = DiffParser.Parse(raw);
        var findings = await Rule.EvaluateAsync(diff, null);

        Assert.Contains(findings, f =>
            f.Summary.Contains("JWT validation weakened") &&
            f.Confidence == Confidence.High);
    }

    [Fact]
    public async Task ValidateLifetimeFalse_ShouldFlagHigh()
    {
        var raw = """
            diff --git a/src/JwtConfig.cs b/src/JwtConfig.cs
            index abc..def 100644
            --- a/src/JwtConfig.cs
            +++ b/src/JwtConfig.cs
            @@ -1,3 +1,4 @@
             var tokenParams = new TokenValidationParameters {
            +    ValidateLifetime = false
             };
            """;

        var diff = DiffParser.Parse(raw);
        var findings = await Rule.EvaluateAsync(diff, null);

        Assert.Contains(findings, f =>
            f.Summary.Contains("JWT validation weakened") &&
            f.Confidence == Confidence.High);
    }

    [Fact]
    public async Task CleanControllerFile_ShouldNotFlag()
    {
        var raw = """
            diff --git a/src/ProductsController.cs b/src/ProductsController.cs
            index abc..def 100644
            --- a/src/ProductsController.cs
            +++ b/src/ProductsController.cs
            @@ -1,3 +1,4 @@
             public class ProductsController : ControllerBase {
            +    private readonly IProductService _svc;
             }
            """;

        var diff = DiffParser.Parse(raw);
        var findings = await Rule.EvaluateAsync(diff, null);

        Assert.Empty(findings);
    }
}
