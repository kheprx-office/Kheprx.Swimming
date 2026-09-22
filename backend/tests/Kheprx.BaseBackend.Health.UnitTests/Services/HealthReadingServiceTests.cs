using Kheprx.BaseBackend.Health.Application.DTOs;
using Kheprx.BaseBackend.Health.Application.Services;
using Kheprx.BaseBackend.Health.Domain.Entities;
using Kheprx.BaseBackend.Health.Domain.Repositories;
using Moq;
using Xunit;

namespace Kheprx.BaseBackend.Health.UnitTests.Services;

public class HealthReadingServiceTests
{
    private static MedicalTest Glucose() =>
        new("Glucose", "الجلوكوز", "mg/dL", 70m, 110m, Guid.NewGuid());

    private static (HealthReadingService svc, Mock<IHealthReadingRepository> readings) Build(MedicalTest? test)
    {
        var tests = new Mock<IMedicalTestRepository>();
        tests.Setup(t => t.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(test);
        var readings = new Mock<IHealthReadingRepository>();
        return (new HealthReadingService(readings.Object, tests.Object), readings);
    }

    [Fact]
    public async Task Create_persists_reading_stamps_recorder_and_returns_dto()
    {
        var test = Glucose();
        var (svc, readings) = Build(test);
        HealthReading? added = null;
        readings.Setup(r => r.AddAsync(It.IsAny<HealthReading>(), It.IsAny<CancellationToken>()))
            .Callback<HealthReading, CancellationToken>((r, _) => added = r)
            .Returns(Task.CompletedTask);
        var recordedBy = Guid.NewGuid();
        var req = new CreateHealthReadingRequest(Guid.NewGuid(), test.Id, 95m);

        var dto = await svc.CreateAsync(req, recordedBy);

        Assert.NotNull(dto);
        Assert.NotNull(added);
        Assert.Equal(recordedBy, added!.RecordedBy);
        Assert.Equal(95m, dto!.Value);
        Assert.NotEqual(default, dto.ReadingDate);
        readings.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Create_derives_normal_status_inside_bounds_and_out_outside()
    {
        var test = Glucose(); // 70..110
        var (svc, _) = Build(test);

        var inside = await svc.CreateAsync(new CreateHealthReadingRequest(Guid.NewGuid(), test.Id, 90m), Guid.NewGuid());
        var low = await svc.CreateAsync(new CreateHealthReadingRequest(Guid.NewGuid(), test.Id, 40m), Guid.NewGuid());
        var boundary = await svc.CreateAsync(new CreateHealthReadingRequest(Guid.NewGuid(), test.Id, 110m), Guid.NewGuid());

        Assert.Equal("normal", inside!.Status);
        Assert.Equal("out", low!.Status);
        Assert.Equal("normal", boundary!.Status); // inclusive upper bound
    }

    [Fact]
    public async Task Create_returns_null_when_test_missing()
    {
        var (svc, readings) = Build(test: null);

        var dto = await svc.CreateAsync(new CreateHealthReadingRequest(Guid.NewGuid(), Guid.NewGuid(), 95m), Guid.NewGuid());

        Assert.Null(dto);
        readings.Verify(r => r.AddAsync(It.IsAny<HealthReading>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    private static (HealthReadingService svc, Mock<IHealthReadingRepository> readings, Mock<IMedicalTestRepository> tests) BuildFull(MedicalTest test)
    {
        var tests = new Mock<IMedicalTestRepository>();
        tests.Setup(t => t.ListAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new[] { test });
        tests.Setup(t => t.GetByIdAsync(test.Id, It.IsAny<CancellationToken>())).ReturnsAsync(test);
        var readings = new Mock<IHealthReadingRepository>();
        return (new HealthReadingService(readings.Object, tests.Object), readings, tests);
    }

    [Fact]
    public async Task List_enriches_rows_with_test_details_and_derives_status()
    {
        var test = Glucose(); // 70..110, unit mg/dL
        var (svc, readings, _) = BuildFull(test);
        var swimmer = Guid.NewGuid();
        var inside = new HealthReading(swimmer, test.Id, 90m, Guid.NewGuid());
        var low = new HealthReading(swimmer, test.Id, 40m, Guid.NewGuid());
        readings.Setup(r => r.ListBySwimmerAsync(swimmer, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { inside, low });

        var list = await svc.ListBySwimmerAsync(swimmer);

        Assert.Equal(2, list.Count);
        Assert.Equal("Glucose", list[0].TestNameEn);
        Assert.Equal("mg/dL", list[0].Unit);
        Assert.Equal(70m, list[0].LowerBound);
        Assert.Equal(110m, list[0].UpperBound);
        Assert.Equal("normal", list[0].Status);
        Assert.Equal("out", list[1].Status);
    }

    [Fact]
    public async Task List_falls_back_when_test_missing_from_catalog()
    {
        var test = Glucose();
        var tests = new Mock<IMedicalTestRepository>();
        tests.Setup(t => t.ListAsync(It.IsAny<CancellationToken>())).ReturnsAsync(Array.Empty<MedicalTest>());
        var readings = new Mock<IHealthReadingRepository>();
        var swimmer = Guid.NewGuid();
        readings.Setup(r => r.ListBySwimmerAsync(swimmer, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { new HealthReading(swimmer, Guid.NewGuid(), 90m, Guid.NewGuid()) });
        var svc = new HealthReadingService(readings.Object, tests.Object);

        var list = await svc.ListBySwimmerAsync(swimmer);

        Assert.Single(list);
        Assert.Equal("unknown", list[0].Status);
        Assert.Equal(string.Empty, list[0].Unit);
    }

    [Fact]
    public async Task Update_changes_value_rederives_status_and_returns_row()
    {
        var test = Glucose(); // 70..110
        var (svc, readings, _) = BuildFull(test);
        var reading = new HealthReading(Guid.NewGuid(), test.Id, 90m, Guid.NewGuid());
        readings.Setup(r => r.GetTrackedAsync(reading.Id, It.IsAny<CancellationToken>())).ReturnsAsync(reading);

        var dto = await svc.UpdateAsync(reading.Id, new UpdateHealthReadingRequest(40m));

        Assert.NotNull(dto);
        Assert.Equal(40m, dto!.Value);
        Assert.Equal("out", dto.Status);
        readings.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Update_returns_null_when_missing()
    {
        var (svc, readings, _) = BuildFull(Glucose());
        readings.Setup(r => r.GetTrackedAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync((HealthReading?)null);

        Assert.Null(await svc.UpdateAsync(Guid.NewGuid(), new UpdateHealthReadingRequest(50m)));
    }

    [Fact]
    public async Task Delete_true_when_found_false_when_missing()
    {
        var (svc, readings, _) = BuildFull(Glucose());
        var reading = new HealthReading(Guid.NewGuid(), Guid.NewGuid(), 90m, Guid.NewGuid());
        readings.Setup(r => r.GetTrackedAsync(reading.Id, It.IsAny<CancellationToken>())).ReturnsAsync(reading);
        readings.Setup(r => r.GetTrackedAsync(It.Is<Guid>(g => g != reading.Id), It.IsAny<CancellationToken>())).ReturnsAsync((HealthReading?)null);

        Assert.True(await svc.DeleteAsync(reading.Id));
        Assert.False(await svc.DeleteAsync(Guid.NewGuid()));
    }
}
