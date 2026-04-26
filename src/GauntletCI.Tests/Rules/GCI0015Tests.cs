// SPDX-License-Identifier: Elastic-2.0
using GauntletCI.Core.Diff;
using GauntletCI.Core.Rules.Implementations;

namespace GauntletCI.Tests.Rules;

public class GCI0015Tests
{
    private static readonly GCI0015_DataIntegrityRisk Rule = new();

    [Fact]
    public async Task UncheckedCastInt_ShouldFlag()
    {
        // Cast must be in a file that also contains HTTP input signals to trigger the rule.
        var raw = """
            diff --git a/src/Controller.cs b/src/Controller.cs
            index abc..def 100644
            --- a/src/Controller.cs
            +++ b/src/Controller.cs
            @@ -1,1 +1,3 @@
             // controller
            +var raw = Request.Form["amount"];
            +var x = (int)userInput;
            """;

        var diff = DiffParser.Parse(raw);
        var findings = await Rule.EvaluateAsync(diff, null);

        Assert.Contains(findings, f => f.Summary.Contains("Unchecked cast"));
    }

    [Fact]
    public async Task SqlIgnorePattern_ShouldFlag()
    {
        var raw = """
            diff --git a/src/Repository.cs b/src/Repository.cs
            index abc..def 100644
            --- a/src/Repository.cs
            +++ b/src/Repository.cs
            @@ -1,1 +1,2 @@
             // repository
            +INSERT IGNORE INTO Users (Name) VALUES (@Name)
            """;

        var diff = DiffParser.Parse(raw);
        var findings = await Rule.EvaluateAsync(diff, null);

        Assert.Contains(findings, f => f.Summary.Contains("INSERT IGNORE"));
    }

    [Fact]
    public async Task MassAssignmentWithoutNullCheck_ShouldFlag()
    {
        // 3+ consecutive entity.Field = request.Field; assignments in an HTTP-context file
        var raw = """
            diff --git a/src/UserService.cs b/src/UserService.cs
            index abc..def 100644
            --- a/src/UserService.cs
            +++ b/src/UserService.cs
            @@ -1,1 +1,7 @@
             // service
            +public IActionResult Create([FromBody] UserInput request)
            +{
            +entity.Name = request.Name;
            +entity.Email = request.Email;
            +entity.Phone = request.Phone;
            +// end assignments
            """;

        var diff = DiffParser.Parse(raw);
        var findings = await Rule.EvaluateAsync(diff, null);

        Assert.Contains(findings, f => f.Summary.Contains("Mass field assignment"));
    }

    [Fact]
    public async Task MassAssignmentInNonHttpFile_ShouldNotFlag()
    {
        // Image/data-format parsers set many fields in sequence -- no HTTP context, no security risk
        var raw = """
            diff --git a/src/ExrAttribute.cs b/src/ExrAttribute.cs
            index abc..def 100644
            --- a/src/ExrAttribute.cs
            +++ b/src/ExrAttribute.cs
            @@ -1,1 +1,6 @@
             // data model
            +this.Width = reader.ReadInt32();
            +this.Height = reader.ReadInt32();
            +this.Depth = reader.ReadInt32();
            +// end
            """;

        var diff = DiffParser.Parse(raw);
        var findings = await Rule.EvaluateAsync(diff, null);

        Assert.DoesNotContain(findings, f => f.Summary.Contains("Mass field assignment"));
    }

    [Fact]
    public async Task MassAssignmentWithNullCheck_ShouldNotFlag()
    {
        var raw = """
            diff --git a/src/UserService.cs b/src/UserService.cs
            index abc..def 100644
            --- a/src/UserService.cs
            +++ b/src/UserService.cs
            @@ -1,1 +1,7 @@
             // service
            +ArgumentNullException.ThrowIfNull(request);
            +entity.Name = request.Name;
            +entity.Email = request.Email;
            +entity.Phone = request.Phone;
            +// end assignments
            """;

        var diff = DiffParser.Parse(raw);
        var findings = await Rule.EvaluateAsync(diff, null);

        Assert.DoesNotContain(findings, f => f.Summary.Contains("Mass field assignment"));
    }

    [Fact]
    public async Task OnConflictDoNothing_ShouldFlag()
    {
        var raw = """
            diff --git a/src/Repository.cs b/src/Repository.cs
            index abc..def 100644
            --- a/src/Repository.cs
            +++ b/src/Repository.cs
            @@ -1,1 +1,2 @@
             // repository
            +INSERT INTO Events (Id, Name) VALUES (@Id, @Name) ON CONFLICT DO NOTHING
            """;

        var diff = DiffParser.Parse(raw);
        var findings = await Rule.EvaluateAsync(diff, null);

        Assert.Contains(findings, f => f.Summary.Contains("ON CONFLICT DO NOTHING"));
    }

    [Fact]
    public async Task HttpInputBinding_WithHttpContextSignal_ShouldFlagHigh()
    {
        var raw = """
            diff --git a/src/UserController.cs b/src/UserController.cs
            index abc..def 100644
            --- a/src/UserController.cs
            +++ b/src/UserController.cs
            @@ -1,1 +1,8 @@
             // controller
            +[HttpPost]
            +public IActionResult Create([FromBody] UserInput input)
            +{
            +entity.Name = input.Name;
            +entity.Email = input.Email;
            +entity.Phone = input.Phone;
            +// end
            """;

        var diff = DiffParser.Parse(raw);
        var findings = await Rule.EvaluateAsync(diff, null);

        Assert.Contains(findings, f =>
            f.Summary.Contains("unsafe HTTP input binding") &&
            f.Confidence == GauntletCI.Core.Model.Confidence.High);
    }

    [Fact]
    public async Task HttpInputBinding_WithoutHttpContextSignal_ShouldNotFlagHigh()
    {
        var raw = """
            diff --git a/src/UserService.cs b/src/UserService.cs
            index abc..def 100644
            --- a/src/UserService.cs
            +++ b/src/UserService.cs
            @@ -1,1 +1,6 @@
             // service
            +entity.Name = model.Name;
            +entity.Email = model.Email;
            +entity.Phone = model.Phone;
            +// end assignments
            """;

        var diff = DiffParser.Parse(raw);
        var findings = await Rule.EvaluateAsync(diff, null);

        Assert.DoesNotContain(findings, f => f.Summary.Contains("unsafe HTTP input binding"));
    }

    [Fact]
    public async Task HttpInputBinding_HttpSignalButFewAssignments_ShouldNotFlag()
    {
        var raw = """
            diff --git a/src/UserController.cs b/src/UserController.cs
            index abc..def 100644
            --- a/src/UserController.cs
            +++ b/src/UserController.cs
            @@ -1,1 +1,5 @@
             // controller
            +public IActionResult Get([FromQuery] string id)
            +{
            +entity.Name = id;
            +// end
            """;

        var diff = DiffParser.Parse(raw);
        var findings = await Rule.EvaluateAsync(diff, null);

        Assert.DoesNotContain(findings, f => f.Summary.Contains("unsafe HTTP input binding"));
    }

    [Fact]
    public async Task UncheckedCastWithoutHttpSignal_ShouldNotFlag()
    {
        // Internal casts without any HTTP input context are not a data integrity risk.
        var raw = """
            diff --git a/src/Service.cs b/src/Service.cs
            index abc..def 100644
            --- a/src/Service.cs
            +++ b/src/Service.cs
            @@ -1,1 +1,2 @@
             // service
            +var count = (int)internalCounter;
            """;

        var diff = DiffParser.Parse(raw);
        var findings = await Rule.EvaluateAsync(diff, null);

        Assert.DoesNotContain(findings, f => f.Summary.Contains("Unchecked cast"));
    }
}
