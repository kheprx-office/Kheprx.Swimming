using Kheprx.BaseBackend.Identity.Domain.Exceptions;

namespace Kheprx.BaseBackend.Identity.Domain.Entities;

public sealed class Moqawel
{
    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public decimal DailyWage { get; private set; }

    private Moqawel() { } // EF Core

    public Moqawel(Guid userId, decimal dailyWage)
    {
        if (dailyWage < 0) throw new InvalidUserException("Daily wage cannot be negative.");
        Id = Guid.NewGuid();
        UserId = userId;
        DailyWage = dailyWage;
    }

    public void Update(decimal dailyWage)
    {
        if (dailyWage < 0) throw new InvalidUserException("Daily wage cannot be negative.");
        DailyWage = dailyWage;
    }
}
