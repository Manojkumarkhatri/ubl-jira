using ArchUnitNET.Domain;
using ArchUnitNET.Loader;
using ArchUnitNET.xUnitV3;
using static ArchUnitNET.Fluent.ArchRuleDefinition;

namespace Upms.Architecture.Tests;

/// <summary>Layer and module boundaries of the modular monolith (research R4, constitution V).</summary>
public sealed class ModuleBoundaryTests
{
    // Phase 4 adds Notifications (research R1): the Work module raises events only through its contract.
    private static readonly string[] Modules = ["Identity", "Projects", "Work", "Notifications"];

    internal static readonly ArchUnitNET.Domain.Architecture Architecture = new ArchLoader()
        .LoadAssemblies(
            typeof(Upms.Domain.Identity.User).Assembly,
            typeof(Upms.Application.Common.ICurrentUser).Assembly,
            typeof(Upms.Infrastructure.Persistence.AppDbContext).Assembly,
            typeof(Program).Assembly)
        .Build();

    [Fact]
    public void The_domain_does_not_depend_on_EF_Core()
    {
        Types().That().ResideInNamespaceMatching(@"^Upms\.Domain(\..+)?$")
            .Should().NotDependOnAny(Types(true).That().ResideInNamespaceMatching(@"^Microsoft\.EntityFrameworkCore(\..+)?$"))
            .Check(Architecture);
    }

    [Fact]
    public void The_domain_does_not_depend_on_ASP_NET_Core_beyond_the_Identity_user_model()
    {
        Types().That().ResideInNamespaceMatching(@"^Upms\.Domain(\..+)?$")
            .Should().NotDependOnAny(Types(true).That().ResideInNamespaceMatching(@"^Microsoft\.AspNetCore\.(?!Identity$).+"))
            .Check(Architecture);
    }

    [Fact]
    public void Domain_modules_do_not_depend_on_each_other()
    {
        foreach (var module in Modules)
        {
            foreach (var other in Modules.Where(m => m != module))
            {
                Types().That().ResideInNamespaceMatching($@"^Upms\.Domain\.{module}(\..+)?$")
                    .Should().NotDependOnAny(Types().That().ResideInNamespaceMatching($@"^Upms\.Domain\.{other}(\..+)?$"))
                    .Because($"the {module} domain must not reach into the {other} domain")
                    .WithoutRequiringPositiveResults() // a module may have no types yet
                    .Check(Architecture);
            }
        }
    }

    [Fact]
    public void Application_modules_use_each_other_only_through_their_contracts()
    {
        foreach (var module in Modules)
        {
            foreach (var other in Modules.Where(m => m != module))
            {
                // Another module's domain types (and therefore its tables) and its non-contract services.
                var forbidden = $@"^Upms\.(Domain\.{other}(\..+)?|Application\.{other}($|\.(?!Contracts($|\.)).+))$";
                Types().That().ResideInNamespaceMatching($@"^Upms\.Application\.{module}(\..+)?$")
                    .Should().NotDependOnAny(Types().That().ResideInNamespaceMatching(forbidden))
                    .Because($"the {module} module may use the {other} module only through Upms.Application.{other}.Contracts")
                    .WithoutRequiringPositiveResults() // a module may have no types yet
                    .Check(Architecture);
            }
        }
    }

    [Fact]
    public void The_Work_module_reaches_the_Notifications_module_through_its_contract()
    {
        // The rule above keeps every other Notifications type out of reach; the contract itself must be public there.
        var center = typeof(Upms.Application.Notifications.Contracts.INotificationCenter);

        Assert.True(center.IsPublic);
        Assert.Equal("Upms.Application.Notifications.Contracts", center.Namespace);
    }

    [Fact]
    public void The_web_layer_uses_infrastructure_only_from_the_composition_root()
    {
        Types().That().ResideInAssembly(typeof(Program).Assembly)
            .And().DoNotHaveNameMatching(@"^Program($|\+)")
            .And().DoNotHaveNameMatching(@"^<")
            .Should().NotDependOnAny(Types().That().ResideInNamespaceMatching(@"^Upms\.Infrastructure(\..+)?$"))
            .Check(Architecture);
    }
}
