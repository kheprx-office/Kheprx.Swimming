using Kheprx.BaseBackend.Identity.Domain.Exceptions;

namespace Kheprx.BaseBackend.Identity.Domain.Entities;

public sealed class Manager
{
    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public decimal MonthlySalary { get; private set; }

    private Manager() { } // EF Core

    public Manager(Guid userId, decimal monthlySalary)
    {
        if (monthlySalary < 0)
            throw new InvalidUserException("Monthly salary cannot be negative.");
        Id = Guid.NewGuid();
        UserId = userId;
        MonthlySalary = monthlySalary;
    }

    public void SetMonthlySalary(decimal value)
    {
        if (value < 0)
            throw new InvalidUserException("Monthly salary cannot be negative.");
        MonthlySalary = value;
    }
}
