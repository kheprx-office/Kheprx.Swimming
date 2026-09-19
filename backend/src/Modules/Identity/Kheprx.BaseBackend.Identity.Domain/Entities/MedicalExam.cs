namespace Kheprx.BaseBackend.Identity.Domain.Entities;

public sealed class MedicalExam
{
    public Guid Id { get; private set; }
    public Guid SwimmerId { get; private set; }
    public DateOnly ExamDate { get; private set; }
    public Guid InternalMedId { get; private set; }
    public Guid HeartAssessId { get; private set; }
    public Guid SpineAssessId { get; private set; }
    public Guid? BloodTypeId { get; private set; }
    public decimal Hemoglobin { get; private set; }
    public decimal HeightCm { get; private set; }
    public decimal WeightKg { get; private set; }
    public DateTime CreatedAt { get; private set; }

    private MedicalExam() { } // EF Core

    public MedicalExam(Guid swimmerId, DateOnly examDate, Guid internalMedId, Guid heartAssessId,
        Guid spineAssessId, Guid? bloodTypeId, decimal hemoglobin, decimal heightCm, decimal weightKg)
    {
        Id = Guid.NewGuid();
        SwimmerId = swimmerId;
        ExamDate = examDate;
        InternalMedId = internalMedId;
        HeartAssessId = heartAssessId;
        SpineAssessId = spineAssessId;
        BloodTypeId = bloodTypeId;
        Hemoglobin = hemoglobin;
        HeightCm = heightCm;
        WeightKg = weightKg;
        CreatedAt = DateTime.UtcNow;
    }

    public void Update(DateOnly examDate, Guid internalMedId, Guid heartAssessId, Guid spineAssessId,
        Guid? bloodTypeId, decimal hemoglobin, decimal heightCm, decimal weightKg)
    {
        ExamDate = examDate;
        InternalMedId = internalMedId;
        HeartAssessId = heartAssessId;
        SpineAssessId = spineAssessId;
        BloodTypeId = bloodTypeId;
        Hemoglobin = hemoglobin;
        HeightCm = heightCm;
        WeightKg = weightKg;
    }
}
