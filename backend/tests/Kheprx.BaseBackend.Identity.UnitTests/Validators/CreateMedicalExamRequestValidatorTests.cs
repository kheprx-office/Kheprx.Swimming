using Kheprx.BaseBackend.Identity.Application.DTOs;
using Kheprx.BaseBackend.Identity.Application.Validators;
using Xunit;

namespace Kheprx.BaseBackend.Identity.UnitTests.Validators;

public class CreateMedicalExamRequestValidatorTests
{
    private readonly CreateMedicalExamRequestValidator _v = new();
    private static CreateMedicalExamRequest Ok() => new(
        DateOnly.FromDateTime(DateTime.UtcNow), null, 15m, 183m, 75m, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());

    [Fact] public void Valid_passes() => Assert.True(_v.Validate(Ok()).IsValid);
    [Fact] public void Empty_assessment_fails() => Assert.False(_v.Validate(Ok() with { InternalMedId = Guid.Empty }).IsValid);
    [Fact] public void Nonpositive_measure_fails() => Assert.False(_v.Validate(Ok() with { HeightCm = 0m }).IsValid);
    [Fact] public void Future_exam_date_fails() => Assert.False(_v.Validate(Ok() with { ExamDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1)) }).IsValid);
}
