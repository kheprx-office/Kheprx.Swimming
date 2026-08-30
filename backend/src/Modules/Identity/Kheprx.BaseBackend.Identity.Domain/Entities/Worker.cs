using Kheprx.BaseBackend.Identity.Domain.Exceptions;

namespace Kheprx.BaseBackend.Identity.Domain.Entities;

public sealed class Worker
{
    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public decimal DailyWage { get; private set; }
    public DateOnly? HireDate { get; private set; }

    private Worker() { } // EF Core

    public Worker(Guid userId, decimal dailyWage, DateOnly? hireDate = null)
    {
        if (dailyWage < 0) throw new InvalidUserException("Daily wage cannot be negative.");
        Id = Guid.NewGuid();
        UserId = userId;
        DailyWage = dailyWage;
        HireDate = hireDate;
    }

    public void Update(decimal dailyWage, DateOnly? hireDate = null)
    {
        if (dailyWage < 0) throw new InvalidUserException("Daily wage cannot be negative.");
        DailyWage = dailyWage;
        HireDate = hireDate;
    }
}
