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

    [Fact]
    public async Task MapsterWithoutValidation_ShouldFlag()
    {
        var raw = """
            diff --git a/src/MappingConfig.cs b/src/MappingConfig.cs
            index abc..def 100644
            --- a/src/MappingConfig.cs
            +++ b/src/MappingConfig.cs
            @@ -1,3 +1,6 @@
             public class MappingConfig : IRegister {
            +    public void Register(TypeAdapterConfig config)
            +    {
            +        config.NewConfig<User, UserDto>();
            +    }
             }
            """;

        var diff = DiffParser.Parse(raw);
        var findings = await Rule.EvaluateAsync(diff, null);

        Assert.Contains(findings, f => f.Summary.Contains("Mapster"));
    }

    [Fact]
    public async Task MapsterWithValidation_ShouldNotFlag()
    {
        var raw = """
            diff --git a/src/MappingConfig.cs b/src/MappingConfig.cs
            index abc..def 100644
            --- a/src/MappingConfig.cs
            +++ b/src/MappingConfig.cs
            @@ -1,3 +1,6 @@
             public class MappingConfig : IRegister {
            +    public void Register(TypeAdapterConfig config)
            +    {
            +        config.NewConfig<User, UserDto>();
            +    }
             }
            diff --git a/src/MappingTests.cs b/src/MappingTests.cs
            index abc..def 100644
            --- a/src/MappingTests.cs
            +++ b/src/MappingTests.cs
            @@ -1,3 +1,4 @@
             public class MappingTests {
            +    TypeAdapterConfig.GlobalSettings.Compile();
             }
            """;

        var diff = DiffParser.Parse(raw);
        var findings = await Rule.EvaluateAsync(diff, null);

        Assert.DoesNotContain(findings, f => f.Summary.Contains("Mapster"));
    }

    [Fact]
    public async Task AgileMapperWithoutValidation_ShouldFlag()
    {
        var raw = """
            diff --git a/src/UserMapping.cs b/src/UserMapping.cs
            index abc..def 100644
            --- a/src/UserMapping.cs
            +++ b/src/UserMapping.cs
            @@ -1,3 +1,6 @@
             using AgileObjects.AgileMapper;
            +public class UserMapping {
            +    Mapper.WhenMapping<User>().To<UserDto>().Map(s => s.FullName).To(d => d.Name);
            +}
            """;

        var diff = DiffParser.Parse(raw);
        var findings = await Rule.EvaluateAsync(diff, null);

        Assert.Contains(findings, f => f.Summary.Contains("AgileMapper"));
    }

    [Fact]
    public async Task TinyMapperBinding_ShouldAlwaysFlag()
    {
        var raw = """
            diff --git a/src/Startup.cs b/src/Startup.cs
            index abc..def 100644
            --- a/src/Startup.cs
            +++ b/src/Startup.cs
            @@ -1,3 +1,4 @@
             public class Startup {
            +    TinyMapper.Bind<User, UserDto>();
             }
            """;

        var diff = DiffParser.Parse(raw);
        var findings = await Rule.EvaluateAsync(diff, null);

        Assert.Contains(findings, f => f.Summary.Contains("TinyMapper"));
    }
}
