using Kheprx.BaseBackend.Identity.Domain.Entities;
using Xunit;

namespace Kheprx.BaseBackend.Identity.UnitTests.Entities;

public class MedicalExamTests
{
    [Fact]
    public void Ctor_assigns_fields_and_stamps_id_and_createdAt()
    {
        var swimmerId = Guid.NewGuid();
        var im = Guid.NewGuid(); var ha = Guid.NewGuid(); var sa = Guid.NewGuid();
        var bt = Guid.NewGuid();

        var exam = new MedicalExam(swimmerId, new DateOnly(2026, 9, 19), im, ha, sa, bt, 14.8m, 182.0m, 74.0m);

        Assert.NotEqual(Guid.Empty, exam.Id);
        Assert.Equal(swimmerId, exam.SwimmerId);
        Assert.Equal(new DateOnly(2026, 9, 19), exam.ExamDate);
        Assert.Equal(im, exam.InternalMedId);
        Assert.Equal(bt, exam.BloodTypeId);
        Assert.Equal(14.8m, exam.Hemoglobin);
        Assert.True(exam.CreatedAt <= DateTime.UtcNow && exam.CreatedAt > DateTime.UtcNow.AddMinutes(-1));
    }

    [Fact]
    public void Ctor_allows_null_blood_type()
    {
        var exam = new MedicalExam(Guid.NewGuid(), new DateOnly(2026, 9, 19),
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), null, 14.0m, 180m, 70m);
        Assert.Null(exam.BloodTypeId);
    }

    [Fact]
    public void Update_changes_mutable_fields_and_keeps_id_swimmer_createdAt()
    {
        var exam = new MedicalExam(Guid.NewGuid(), new DateOnly(2024, 1, 1),
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), null, 13.0m, 178m, 71m);
        var id = exam.Id; var swimmerId = exam.SwimmerId; var createdAt = exam.CreatedAt;
        var im = Guid.NewGuid(); var ha = Guid.NewGuid(); var sa = Guid.NewGuid(); var bt = Guid.NewGuid();

        exam.Update(new DateOnly(2026, 9, 19), im, ha, sa, bt, 15.0m, 183m, 75m);

        Assert.Equal(new DateOnly(2026, 9, 19), exam.ExamDate);
        Assert.Equal(im, exam.InternalMedId);
        Assert.Equal(ha, exam.HeartAssessId);
        Assert.Equal(sa, exam.SpineAssessId);
        Assert.Equal(bt, exam.BloodTypeId);
        Assert.Equal(15.0m, exam.Hemoglobin);
        Assert.Equal(183m, exam.HeightCm);
        Assert.Equal(75m, exam.WeightKg);
        Assert.Equal(id, exam.Id);
        Assert.Equal(swimmerId, exam.SwimmerId);
        Assert.Equal(createdAt, exam.CreatedAt);
    }
}
