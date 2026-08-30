using System.Reflection;
using Kheprx.BaseBackend.Api.Controllers;
using Microsoft.AspNetCore.Authorization;
using NetArchTest.Rules;
using Xunit;

namespace Kheprx.BaseBackend.ArchitectureTests;

public class BoundaryTests
{
    // Generic layering rules (Domain → no Infrastructure/EF, Application → no Infrastructure/EF,
    // Domain → no Application, Infrastructure → no Api) are anchored to the Identity module as the
    // only remaining Domain/Application/Infrastructure triad after the business-module strip.
    private static readonly Assembly Domain =
        typeof(Kheprx.BaseBackend.Identity.Domain.Entities.User).Assembly;
    private static readonly Assembly Application =
        typeof(Kheprx.BaseBackend.Identity.Application.Services.Interfaces.IAuthService).Assembly;
    private static readonly Assembly Infrastructure =
        typeof(Kheprx.BaseBackend.Identity.Infrastructure.Data.IdentityDbContext).Assembly;
    private static readonly Assembly Api = typeof(BaseApiController).Assembly;

    [Fact]
    public void Domain_should_not_depend_on_Infrastructure()
    {
        var result = Types.InAssembly(Domain)
            .ShouldNot().HaveDependencyOn("Kheprx.BaseBackend.Identity.Infrastructure")
            .GetResult();
        Assert.True(result.IsSuccessful, Describe(result));
    }

    [Fact]
    public void Domain_should_not_depend_on_EntityFrameworkCore()
    {
        var result = Types.InAssembly(Domain)
            .ShouldNot().HaveDependencyOn("Microsoft.EntityFrameworkCore")
            .GetResult();
        Assert.True(result.IsSuccessful, Describe(result));
    }

    [Fact]
    public void Application_should_not_depend_on_Infrastructure()
    {
        var result = Types.InAssembly(Application)
            .ShouldNot().HaveDependencyOn("Kheprx.BaseBackend.Identity.Infrastructure")
            .GetResult();
        Assert.True(result.IsSuccessful, Describe(result));
    }

    [Fact]
    public void Application_should_not_depend_on_EntityFrameworkCore()
    {
        var result = Types.InAssembly(Application)
            .ShouldNot().HaveDependencyOn("Microsoft.EntityFrameworkCore")
            .GetResult();
        Assert.True(result.IsSuccessful, Describe(result));
    }

    [Fact]
    public void Domain_should_not_depend_on_Application()
    {
        var result = Types.InAssembly(Domain)
            .ShouldNot().HaveDependencyOn("Kheprx.BaseBackend.Identity.Application")
            .GetResult();
        Assert.True(result.IsSuccessful, Describe(result));
    }

    [Fact]
    public void Infrastructure_should_not_depend_on_Api()
    {
        var result = Types.InAssembly(Infrastructure)
            .ShouldNot().HaveDependencyOn("Kheprx.BaseBackend.Api")
            .GetResult();
        Assert.True(result.IsSuccessful, Describe(result));
    }

    [Fact]
    public void Controllers_should_inherit_BaseApiController()
    {
        var result = Types.InAssembly(Api)
            .That().HaveNameEndingWith("Controller")
            .And().AreClasses()
            .And().AreNotAbstract()
            .Should().Inherit(typeof(BaseApiController))
            .GetResult();
        Assert.True(result.IsSuccessful, Describe(result));
    }

    // ASP.NET Core ANDs a class-level [Authorize(Roles=...)] with an action-level one, so a
    // controller that restricts roles on the class can never serve an action scoped to a
    // different role — the caller would have to hold both. AttendanceController shipped that
    // way and made every worker endpoint (clock-in, clock-out, me/today) permanently 403.
    // Controller unit tests instantiate the class directly and never evaluate attributes, so
    // only a reflection check like this one can catch it.
    [Fact]
    public void Controllers_with_action_level_roles_should_not_also_restrict_roles_on_the_class()
    {
        var offenders = Api.GetTypes()
            .Where(t => t.Name.EndsWith("Controller") && t.IsClass && !t.IsAbstract)
            .Select(t => new
            {
                Controller = t,
                ClassRoles = t.GetCustomAttributes(typeof(AuthorizeAttribute), inherit: true)
                    .Cast<AuthorizeAttribute>()
                    .Select(a => a.Roles)
                    .FirstOrDefault(r => !string.IsNullOrWhiteSpace(r)),
                ActionRoleSets = t.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                    .SelectMany(m => m.GetCustomAttributes(typeof(AuthorizeAttribute), inherit: true)
                        .Cast<AuthorizeAttribute>())
                    .Select(a => a.Roles)
                    .Where(r => !string.IsNullOrWhiteSpace(r))
                    .ToArray(),
            })
            .Where(x => x.ClassRoles is not null && x.ActionRoleSets.Length > 0)
            .Select(x => $"{x.Controller.Name}: class requires [{x.ClassRoles}] but actions also " +
                         $"scope roles [{string.Join(" | ", x.ActionRoleSets)}] — these are ANDed")
            .ToArray();

        Assert.True(offenders.Length == 0, string.Join(Environment.NewLine, offenders));
    }

    [Fact]
    public void Identity_Domain_should_not_depend_on_Infrastructure()
    {
        var result = Types.InAssembly(Domain)
            .ShouldNot().HaveDependencyOn("Kheprx.BaseBackend.Identity.Infrastructure")
            .GetResult();
        Assert.True(result.IsSuccessful, Describe(result));
    }

    [Fact]
    public void Identity_Domain_should_not_depend_on_EntityFrameworkCore()
    {
        var result = Types.InAssembly(Domain)
            .ShouldNot().HaveDependencyOn("Microsoft.EntityFrameworkCore")
            .GetResult();
        Assert.True(result.IsSuccessful, Describe(result));
    }

    [Fact]
    public void Identity_Application_should_not_depend_on_Infrastructure()
    {
        var result = Types.InAssembly(Application)
            .ShouldNot().HaveDependencyOn("Kheprx.BaseBackend.Identity.Infrastructure")
            .GetResult();
        Assert.True(result.IsSuccessful, Describe(result));
    }

    private static string Describe(TestResult result)
        => result.IsSuccessful ? "" : string.Join(", ", result.FailingTypeNames ?? new List<string>());
}
