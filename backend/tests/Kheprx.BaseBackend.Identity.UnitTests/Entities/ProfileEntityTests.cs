using Kheprx.BaseBackend.Identity.Domain.Entities;
using Kheprx.BaseBackend.Identity.Domain.Exceptions;
using Xunit;

namespace Kheprx.BaseBackend.Identity.UnitTests.Entities;

public class ProfileEntityTests
{
    [Fact]
    public void Manager_rejects_negative_salary()
    {
        Assert.Throws<InvalidUserException>(() => new Manager(Guid.NewGuid(), -1m));
        var manager = new Manager(Guid.NewGuid(), 0m);
        Assert.Throws<InvalidUserException>(() => manager.SetMonthlySalary(-5m));
    }

    [Fact]
    public void Manager_sets_and_updates_salary()
    {
        var userId = Guid.NewGuid();
        var manager = new Manager(userId, 15000m);
        Assert.Equal(userId, manager.UserId);
        Assert.Equal(15000m, manager.MonthlySalary);
        manager.SetMonthlySalary(16000m);
        Assert.Equal(16000m, manager.MonthlySalary);
    }

    [Fact]
    public void Moqawel_rejects_negative_wage_and_updates()
    {
        Assert.Throws<InvalidUserException>(() => new Moqawel(Guid.NewGuid(), -1m));
        var moqawel = new Moqawel(Guid.NewGuid(), 500m);
        moqawel.Update(600m);
        Assert.Equal(600m, moqawel.DailyWage);
        Assert.Throws<InvalidUserException>(() => moqawel.Update(-1m));
    }

    [Fact]
    public void Worker_holds_hire_date_and_updates()
    {
        var hired = new DateOnly(2026, 1, 15);
        var worker = new Worker(Guid.NewGuid(), 350m, hired);
        Assert.Equal(hired, worker.HireDate);
        worker.Update(400m, null);
        Assert.Null(worker.HireDate);
        Assert.Equal(400m, worker.DailyWage);
        Assert.Throws<InvalidUserException>(() => new Worker(Guid.NewGuid(), -1m));
    }
}
