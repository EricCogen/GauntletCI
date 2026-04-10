// SPDX-License-Identifier: Elastic-2.0
using GauntletCI.Core.Diff;
using GauntletCI.Core.Rules.Implementations;

namespace GauntletCI.Tests.Rules;

public class GCI0037Tests
{
    private static readonly GCI0037_AutoMapperIntegrity Rule = new();

    [Fact]
    public async Task CreateMapWithoutValidation_ShouldFlag()
    {
        var raw = """
            diff --git a/src/MappingProfile.cs b/src/MappingProfile.cs
            index abc..def 100644
            --- a/src/MappingProfile.cs
            +++ b/src/MappingProfile.cs
            @@ -1,4 +1,6 @@
             public class MappingProfile : Profile {
            +    CreateMap<User, UserDto>();
            +    CreateMap<Order, OrderDto>();
             }
            """;

        var diff = DiffParser.Parse(raw);
        var findings = await Rule.EvaluateAsync(diff, null);

        Assert.Contains(findings, f => f.Summary.Contains("AutoMapper") && f.Summary.Contains("AssertConfigurationIsValid"));
    }

    [Fact]
    public async Task ProfileInheritanceWithoutValidation_ShouldFlag()
    {
        var raw = """
            diff --git a/src/UserProfile.cs b/src/UserProfile.cs
            index abc..def 100644
            --- a/src/UserProfile.cs
            +++ b/src/UserProfile.cs
            @@ -1,3 +1,4 @@
            +public class UserProfile : Profile {
            +    CreateMap<User, UserDto>();
            +}
             // end
            """;

        var diff = DiffParser.Parse(raw);
        var findings = await Rule.EvaluateAsync(diff, null);

        Assert.NotEmpty(findings);
    }

    [Fact]
    public async Task CreateMapWithValidation_ShouldNotFlag()
    {
        var raw = """
            diff --git a/src/MappingProfile.cs b/src/MappingProfile.cs
            index abc..def 100644
            --- a/src/MappingProfile.cs
            +++ b/src/MappingProfile.cs
            @@ -1,4 +1,5 @@
             public class MappingProfile : Profile {
            +    CreateMap<User, UserDto>();
             }
            diff --git a/src/MappingTests.cs b/src/MappingTests.cs
            index abc..def 100644
            --- a/src/MappingTests.cs
            +++ b/src/MappingTests.cs
            @@ -1,3 +1,4 @@
             public class MappingTests {
            +    config.AssertConfigurationIsValid();
             }
            """;

        var diff = DiffParser.Parse(raw);
        var findings = await Rule.EvaluateAsync(diff, null);

        Assert.Empty(findings);
    }

    [Fact]
    public async Task NoAutoMapperSignals_ShouldNotFlag()
    {
        var raw = """
            diff --git a/src/Service.cs b/src/Service.cs
            index abc..def 100644
            --- a/src/Service.cs
            +++ b/src/Service.cs
            @@ -1,3 +1,4 @@
             public class Service {
            +    return new UserDto { Name = user.Name };
             }
            """;

        var diff = DiffParser.Parse(raw);
        var findings = await Rule.EvaluateAsync(diff, null);

        Assert.Empty(findings);
    }
}
