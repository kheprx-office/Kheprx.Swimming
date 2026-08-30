using FluentValidation;

namespace Kheprx.BaseBackend.Api.Extensions.Validation;

public static class FluentValidationExtensions
{
    /// <summary>
    /// Registers FluentValidation validators from every loaded Kheprx.BaseBackend module assembly.
    /// Call after the AddIdentityModule registration so the AppDomain scan sees the module assemblies.
    /// </summary>
    public static IServiceCollection AddFluentValidationConfiguration(this IServiceCollection services)
    {
        // Ask the runtime for every assembly (DLL) that is ALREADY loaded into this
        // process, then keep only ours: the ones whose name starts with
        // "Kheprx.BaseBackend." (Identity.Application, ...).
        // This is why call order in Program.cs matters - the AddIdentityModule
        // call loads the module DLLs first (their registration code references types
        // in those assemblies), so they are all in this list by the time we scan.
        // Filter details: GetName().Name is the simple assembly name; "?." + "== true"
        // treats a null name as "not ours" instead of crashing; Ordinal is a plain
        // character-by-character match (no culture rules).
        var moduleAssemblies = AppDomain.CurrentDomain.GetAssemblies()
            .Where(a => a.GetName().Name?.StartsWith("Kheprx.BaseBackend.", StringComparison.Ordinal) == true)
            .ToArray();

        // Hand those assemblies to FluentValidation: it scans each one for classes that
        // inherit AbstractValidator<T> (LoginRequestValidator, ...) and registers each
        // in DI as IValidator<T>, so ValidationFilter can resolve the right validator
        // for each incoming request DTO. includeInternalTypes: true widens the scan to
        // internal classes too - our validators are public today, but module
        // implementation types are conventionally internal here (AuthService is one),
        // so the flag keeps the scan safe if validators follow that convention.
        services.AddValidatorsFromAssemblies(moduleAssemblies, includeInternalTypes: true);

        // Return the same collection - the standard fluent shape for extension methods.
        return services;
    }
}
